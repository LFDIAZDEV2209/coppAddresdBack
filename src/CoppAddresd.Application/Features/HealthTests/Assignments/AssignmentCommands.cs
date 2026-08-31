using CoppAddresd.Application.Features.HealthTests;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using MediatR;

namespace CoppAddresd.Application.Features.HealthTests.Assignments;

// --- List Batteries ---

public record ListBatteriesQuery(
    string? Search = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<PaginatedHealthTestsResult<HealthTestBatteryDto>>;

public sealed class ListBatteriesQueryHandler(IHealthTestRepository repository)
    : IRequestHandler<ListBatteriesQuery, PaginatedHealthTestsResult<HealthTestBatteryDto>>
{
    public async Task<PaginatedHealthTestsResult<HealthTestBatteryDto>> Handle(
        ListBatteriesQuery request,
        CancellationToken ct
    )
    {
        var (items, total) = await repository.ListBatteriesAsync(
            request.Search,
            request.IsActive,
            Math.Max(1, request.Page),
            Math.Clamp(request.PageSize, 1, 100),
            ct
        );
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return new PaginatedHealthTestsResult<HealthTestBatteryDto>(
            items.Select(HealthTestBatteryDto.FromEntity).ToList(),
            total,
            Math.Max(1, request.Page),
            pageSize,
            totalPages
        );
    }
}

// --- Create Battery ---

public record BatteryItemInput(
    Guid InstrumentId,
    Guid? VersionId,
    int SortOrder,
    bool IsRequired,
    int? FrequencyDays
);

public record CreateBatteryRequest(
    string Code,
    string Name,
    string? Description,
    bool AutoAssignOnPatientCreate,
    IReadOnlyList<BatteryItemInput> Items
);

public record CreateBatteryCommand(CreateBatteryRequest Request) : IRequest<HealthTestBatteryDto>;

public sealed class CreateBatteryCommandHandler(IHealthTestRepository repository)
    : IRequestHandler<CreateBatteryCommand, HealthTestBatteryDto>
{
    public async Task<HealthTestBatteryDto> Handle(
        CreateBatteryCommand request,
        CancellationToken ct
    )
    {
        var r = request.Request;
        var battery = new HealthTestBattery
        {
            Id = Guid.NewGuid(),
            Code = r.Code.Trim(),
            Name = r.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim(),
            AutoAssignOnPatientCreate = r.AutoAssignOnPatientCreate,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        await repository.AddBatteryAsync(battery, ct);

        if (r.Items is { Count: > 0 })
        {
            var items = r
                .Items.Select(i => new HealthTestBatteryItem
                {
                    Id = Guid.NewGuid(),
                    BatteryId = battery.Id,
                    InstrumentId = i.InstrumentId,
                    VersionId = i.VersionId,
                    SortOrder = i.SortOrder,
                    IsRequired = i.IsRequired,
                    FrequencyDays = i.FrequencyDays,
                })
                .ToList();
            await repository.AddBatteryItemsRangeAsync(items, ct);
        }

        return HealthTestBatteryDto.FromEntity(
            (await repository.GetBatteryWithItemsAsync(battery.Id, ct))!
        );
    }
}

// --- Assign Battery to Patients (individual y masiva) ---

public record AssignBatteryCommand(
    Guid BatteryId,
    IReadOnlyList<Guid> PatientIds,
    Guid? AssignedBy = null
) : IRequest<IReadOnlyList<HealthTestAssignmentDto>>;

public sealed class AssignBatteryCommandHandler(IHealthTestRepository repository)
    : IRequestHandler<AssignBatteryCommand, IReadOnlyList<HealthTestAssignmentDto>>
{
    public async Task<IReadOnlyList<HealthTestAssignmentDto>> Handle(
        AssignBatteryCommand request,
        CancellationToken ct
    )
    {
        var battery = await repository.GetBatteryWithItemsAsync(request.BatteryId, ct);
        if (battery is null)
        {
            throw new InvalidOperationException("La batería no existe.");
        }

        if (!battery.IsActive)
        {
            throw new InvalidOperationException("La batería está desactivada.");
        }

        var assignments = new List<HealthTestAssignment>();
        var now = DateTime.UtcNow;

        foreach (var patientId in request.PatientIds)
        {
            // Batería asignada (agrupación semántica para la mobile).
            var batteryAssignment = new HealthTestBatteryAssignment
            {
                Id = Guid.NewGuid(),
                PatientId = patientId,
                BatteryId = battery.Id,
                Status = HealthTestAssignmentStatus.pending,
                AssignedBy = request.AssignedBy,
                AssignedAt = now,
            };
            await repository.AddBatteryAssignmentAsync(batteryAssignment, ct);

            foreach (var item in battery.Items.OrderBy(i => i.SortOrder))
            {
                var versionId =
                    item.VersionId ?? await ResolveActiveVersionAsync(item.InstrumentId, ct);
                if (versionId is null)
                {
                    // Sin versión activa: no se asigna (requisito: solo versiones publicables).
                    continue;
                }

                var assignment = new HealthTestAssignment
                {
                    Id = Guid.NewGuid(),
                    PatientId = patientId,
                    BatteryAssignmentId = batteryAssignment.Id,
                    VersionId = versionId.Value,
                    Status = HealthTestAssignmentStatus.pending,
                    AssignedBy = request.AssignedBy,
                    AssignedAt = now,
                };
                await repository.AddAssignmentAsync(assignment, ct);
                assignments.Add(assignment);
            }
        }

        return assignments.Select(HealthTestAssignmentDto.FromEntity).ToList();
    }

    private async Task<Guid?> ResolveActiveVersionAsync(Guid instrumentId, CancellationToken ct)
    {
        var versions = await repository.ListVersionsByInstrumentAsync(instrumentId, ct);
        return versions
            .FirstOrDefault(v => v.IsCurrent && v.Status == HealthTestVersionStatus.active)
            ?.Id;
    }
}

// --- Assign Single Test to Patient ---

public record AssignTestCommand(
    Guid PatientId,
    Guid VersionId,
    int? Priority = null,
    DateTime? DueDate = null,
    Guid? AssignedBy = null
) : IRequest<HealthTestAssignmentDto>;

public sealed class AssignTestCommandHandler(IHealthTestRepository repository)
    : IRequestHandler<AssignTestCommand, HealthTestAssignmentDto>
{
    public async Task<HealthTestAssignmentDto> Handle(
        AssignTestCommand request,
        CancellationToken ct
    )
    {
        if (!await repository.PatientExistsAsync(request.PatientId, ct))
        {
            throw new InvalidOperationException("El paciente no existe.");
        }

        var version = await repository.GetVersionByIdAsync(request.VersionId, ct);
        if (version is null)
        {
            throw new InvalidOperationException("La versión del test no existe.");
        }

        if (version.Status != HealthTestVersionStatus.active)
        {
            // Requisito: no se asigna una versión Draft/Retired.
            throw new InvalidOperationException(
                "Solo se asigna una versión activa (Active) del test."
            );
        }

        var assignment = new HealthTestAssignment
        {
            Id = Guid.NewGuid(),
            PatientId = request.PatientId,
            VersionId = request.VersionId,
            Status = HealthTestAssignmentStatus.pending,
            Priority = request.Priority,
            AssignedBy = request.AssignedBy,
            AssignedAt = DateTime.UtcNow,
            DueDate = request.DueDate,
        };
        await repository.AddAssignmentAsync(assignment, ct);
        return HealthTestAssignmentDto.FromEntity(assignment);
    }
}

// --- Cancel Assignment ---

public record CancelAssignmentCommand(Guid AssignmentId, Guid? ActorId = null)
    : IRequest<HealthTestAssignmentDto?>;

public sealed class CancelAssignmentCommandHandler(IHealthTestRepository repository)
    : IRequestHandler<CancelAssignmentCommand, HealthTestAssignmentDto?>
{
    public async Task<HealthTestAssignmentDto?> Handle(
        CancelAssignmentCommand request,
        CancellationToken ct
    )
    {
        var assignment = await repository.GetAssignmentByIdAsync(request.AssignmentId, ct);
        if (assignment is null)
        {
            return null;
        }

        assignment.Status = HealthTestAssignmentStatus.cancelled;
        await repository.UpdateAssignmentAsync(assignment, ct);
        return HealthTestAssignmentDto.FromEntity(assignment);
    }
}

// --- List Assignments (ERP) ---

public record ListPendingAssignmentsForProfessionalQuery(
    IReadOnlyCollection<Guid> PatientIds,
    string? Status = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<PaginatedHealthTestsResult<HealthTestAssignmentDto>>;

public sealed class ListPendingAssignmentsForProfessionalQueryHandler(
    IHealthTestRepository repository
)
    : IRequestHandler<
        ListPendingAssignmentsForProfessionalQuery,
        PaginatedHealthTestsResult<HealthTestAssignmentDto>
    >
{
    public async Task<PaginatedHealthTestsResult<HealthTestAssignmentDto>> Handle(
        ListPendingAssignmentsForProfessionalQuery request,
        CancellationToken ct
    )
    {
        var (items, total) = await repository.ListPendingAssignmentsForProfessionalAsync(
            request.PatientIds,
            request.Status,
            Math.Max(1, request.Page),
            Math.Clamp(request.PageSize, 1, 100),
            ct
        );
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return new PaginatedHealthTestsResult<HealthTestAssignmentDto>(
            items.Select(HealthTestAssignmentDto.FromEntity).ToList(),
            total,
            Math.Max(1, request.Page),
            pageSize,
            totalPages
        );
    }
}

public record ListAssignmentsQuery(
    Guid? PatientId = null,
    Guid? VersionId = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<PaginatedHealthTestsResult<HealthTestAssignmentDto>>;

public sealed class ListAssignmentsQueryHandler(IHealthTestRepository repository)
    : IRequestHandler<ListAssignmentsQuery, PaginatedHealthTestsResult<HealthTestAssignmentDto>>
{
    public async Task<PaginatedHealthTestsResult<HealthTestAssignmentDto>> Handle(
        ListAssignmentsQuery request,
        CancellationToken ct
    )
    {
        var (items, total) = await repository.ListAssignmentsAsync(
            request.PatientId,
            request.VersionId,
            request.Status,
            Math.Max(1, request.Page),
            Math.Clamp(request.PageSize, 1, 100),
            ct
        );
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return new PaginatedHealthTestsResult<HealthTestAssignmentDto>(
            items.Select(HealthTestAssignmentDto.FromEntity).ToList(),
            total,
            Math.Max(1, request.Page),
            pageSize,
            totalPages
        );
    }
}

// --- Auto-assign inicial (hook al crear paciente) ---

public record AutoAssignInitialBatteryCommand(Guid PatientId, Guid? ActorId = null) : IRequest<int>;

public sealed class AutoAssignInitialBatteryCommandHandler(IHealthTestRepository repository)
    : IRequestHandler<AutoAssignInitialBatteryCommand, int>
{
    public async Task<int> Handle(AutoAssignInitialBatteryCommand request, CancellationToken ct)
    {
        var battery = await repository.GetAutoAssignBatteryAsync(ct);
        if (battery is null)
        {
            return 0;
        }

        // Re-asignar cada N días (frequency_days): no duplica si ya hay una
        // asignación pendiente/in-progress para el mismo (batería, paciente).
        var existing = await repository.ListBatteryAssignmentsByPatientAsync(request.PatientId, ct);
        if (
            existing.Any(a =>
                a.BatteryId == battery.Id
                && (
                    a.Status == HealthTestAssignmentStatus.pending
                    || a.Status == HealthTestAssignmentStatus.in_progress
                )
            )
        )
        {
            return 0;
        }

        var command = new AssignBatteryCommand(battery.Id, [request.PatientId], request.ActorId);
        var handler = new AssignBatteryCommandHandler(repository);
        var assignments = await handler.Handle(command, ct);
        return assignments.Count;
    }
}
