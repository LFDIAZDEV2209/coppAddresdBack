using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Professionals;

// ─── DTOs de entrada ──────────────────────────────────────────────────

/// <summary>
/// Entrada de clínica por código dentro de una fila del CSV de empleados.
/// Code y RoleName son obligatorios juntos: si se indica clínica se debe
/// asignar un rol (con scope de clínica) al empleado invitado.
/// </summary>
public record EmployeeBulkClinicInput(string Code, string RoleName);

/// <summary>Fila individual del CSV importado.</summary>
public record BulkEmployeeRowInput(
    string FirstName,
    string LastName,
    string Email,
    string? ProfessionalTypeName,
    string Status,
    IReadOnlyList<EmployeeBulkClinicInput>? Clinics = null);

/// <summary>Envelope de la solicitud de creación masiva.</summary>
public record BulkCreateEmployeesRequest(
    Guid OrganizationId,
    IReadOnlyList<BulkEmployeeRowInput> Rows);

// ─── DTOs de salida ───────────────────────────────────────────────────

/// <summary>Resultado de una fila individual.</summary>
public record BulkRowResultDto(
    int Line,
    bool Success,
    Guid? EmployeeId = null,
    Guid? UserId = null,
    string? Error = null);

/// <summary>Resultado global de la operación masiva.</summary>
public record BulkCreateResultDto(
    IReadOnlyList<BulkRowResultDto> Results,
    int Created,
    int Failed);

// ─── Comando MediatR ─────────────────────────────────────────────────

public record BulkCreateEmployeesCommand(
    Guid OrganizationId,
    IReadOnlyList<BulkEmployeeRowInput> Rows) : IRequest<BulkCreateResultDto>;

// ─── Validador FluentValidation (envelope) ───────────────────────────

public sealed class BulkCreateEmployeesValidator : AbstractValidator<BulkCreateEmployeesCommand>
{
    private const int MaxRows = 500;

    public BulkCreateEmployeesValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("La organización es requerida.");

        RuleFor(x => x.Rows)
            .NotEmpty().WithMessage("La lista de filas no puede estar vacía.")
            .Must(rows => rows.Count <= MaxRows)
            .WithMessage($"El lote no puede superar {MaxRows} filas.");

        // Validación de clínicas por fila: si se provee una lista de clínicas,
        // cada elemento debe tener Code y RoleName no vacíos (requeridos juntos).
        RuleForEach(x => x.Rows)
            .ChildRules(row =>
            {
                row.RuleFor(r => r.Clinics)
                    .Must(clinics => clinics is null || clinics.All(c =>
                        !string.IsNullOrWhiteSpace(c.Code) && !string.IsNullOrWhiteSpace(c.RoleName)))
                    .When(r => r.Clinics is { Count: > 0 })
                    .WithMessage("Cada clínica debe tener Code y RoleName no vacíos.");
            });
    }
}

// ─── Handler ──────────────────────────────────────────────────────────

public sealed class BulkCreateEmployeesCommandHandler(
    IMediator mediator,
    IEmployeeRepository employeeRepository,
    IOrganizationRepository organizationRepository,
    IAuthRolesClient authRolesClient,
    ILogger<BulkCreateEmployeesCommandHandler> logger)
    : IRequestHandler<BulkCreateEmployeesCommand, BulkCreateResultDto>
{
    private static readonly Dictionary<string, string> StatusMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["activo"] = "Active",
        ["invitado"] = "Invited",
        ["inactivo"] = "Inactive",
    };

    public async Task<BulkCreateResultDto> Handle(
        BulkCreateEmployeesCommand request,
        CancellationToken ct)
    {
        // 1. Validar organización (una sola vez, antes de procesar filas).
        var organization = await organizationRepository.GetOrganizationByIdAsync(
            request.OrganizationId, ct);

        if (organization is null)
        {
            // Si la organización no existe, todas las filas fallan.
            var allFailed = request.Rows
                .Select((_, i) => new BulkRowResultDto(i + 1, false, Error: "La organización no existe."))
                .ToList();
            return new BulkCreateResultDto(allFailed, 0, allFailed.Count);
        }

        // 2. Cargar catálogo de tipos profesionales una sola vez.
        var professionalTypes = await organizationRepository.ListProfessionalTypesAsync(ct);
        var typesByName = professionalTypes
            .Where(t => t.IsActive)
            .ToDictionary(
                t => t.Name.Trim(),
                t => t,
                StringComparer.OrdinalIgnoreCase);

        // 3. Precargar códigos de clínica del batch para resolver en una sola query.
        var allClinicCodes = request.Rows
            .Where(r => r.Clinics is { Count: > 0 })
            .SelectMany(r => r.Clinics!)
            .Select(c => c.Code.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Dictionary<string, Guid> clinicCodeToId = new(StringComparer.OrdinalIgnoreCase);
        if (allClinicCodes.Count > 0)
        {
            var clinicResults = await organizationRepository.GetClinicsByCodesAsync(
                request.OrganizationId, allClinicCodes, ct);

            // Mapear cada código a su Id (múltiples códigos pueden resolver a la misma clínica).
            foreach (var (id, code) in clinicResults)
            {
                clinicCodeToId[code.Trim()] = id;
            }
        }

        // 4. Cache de roles resueltos por nombre (evita N llamadas al Auth para el mismo rol).
        Dictionary<string, AuthRoleLookupResult?> roleCache = new(StringComparer.OrdinalIgnoreCase);

        // 5. Acumular emails del batch para detectar duplicados internos.
        var emailsInBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<BulkRowResultDto>();

        // 6. Procesar cada fila de forma independiente.
        for (var i = 0; i < request.Rows.Count; i++)
        {
            var line = i + 1; // 1-based
            var row = request.Rows[i];

            var rowResult = await ProcessRowAsync(
                row, line, organization, typesByName, clinicCodeToId, roleCache, emailsInBatch, ct);

            results.Add(rowResult);
        }

        var created = results.Count(r => r.Success);
        var failed = results.Count(r => !r.Success);

        logger.LogInformation(
            "Bulk create completado para org {OrgId}: {Created} creados, {Failed} fallidos de {Total} filas.",
            request.OrganizationId, created, failed, request.Rows.Count);

        return new BulkCreateResultDto(results, created, failed);
    }

    private async Task<BulkRowResultDto> ProcessRowAsync(
        BulkEmployeeRowInput row,
        int line,
        Organization organization,
        Dictionary<string, ProfessionalType> typesByName,
        Dictionary<string, Guid> clinicCodeToId,
        Dictionary<string, AuthRoleLookupResult?> roleCache,
        HashSet<string> emailsInBatch,
        CancellationToken ct)
    {
        // ── Validaciones por fila ──────────────────────────────────────

        // firstName requerido
        if (string.IsNullOrWhiteSpace(row.FirstName))
            return Fail(line, "El nombre es requerido.");

        // lastName requerido
        if (string.IsNullOrWhiteSpace(row.LastName))
            return Fail(line, "Los apellidos son requeridos.");

        // email requerido
        if (string.IsNullOrWhiteSpace(row.Email))
            return Fail(line, "El correo es requerido.");

        var email = row.Email.Trim().ToLowerInvariant();

        // email formato válido
        if (!IsValidEmail(email))
            return Fail(line, "El correo no tiene un formato válido.");

        // email duplicado dentro del batch
        if (!emailsInBatch.Add(email))
            return Fail(line, $"El correo '{email}' está duplicado en el lote.");

        // email ya existente en la organización
        if (await employeeRepository.EmailExistsInOrganizationAsync(
                organization.Id, email, ct: ct))
            return Fail(line, $"Ya existe un empleado con el correo '{email}' en esta organización.");

        // status válido
        if (string.IsNullOrWhiteSpace(row.Status))
            return Fail(line, "El estado es requerido.");

        if (!StatusMap.TryGetValue(row.Status.Trim(), out var mappedStatus))
            return Fail(line, $"El estado '{row.Status}' no es válido. Valores permitidos: activo, invitado, inactivo.");

        // professionalTypeName (opcional): resolver contra catálogo
        Guid? professionalTypeId = null;
        var typeName = row.ProfessionalTypeName?.Trim();
        if (!string.IsNullOrWhiteSpace(typeName))
        {
            if (!typesByName.TryGetValue(typeName, out var matchedType))
                return Fail(line, $"El tipo de profesional '{typeName}' no existe en el catálogo.");

            professionalTypeId = matchedType.Id;
        }

        // ── Resolver clínicas por código + roles ───────────────────────

        List<ClinicAssignmentInput>? clinicAssignments = null;
        List<ScopedRoleAssignmentInput>? scopedRoles = null;

        if (row.Clinics is { Count: > 0 })
        {
            clinicAssignments = [];
            scopedRoles = [];

            for (var ci = 0; ci < row.Clinics.Count; ci++)
            {
                var clinicInput = row.Clinics[ci];
                var code = clinicInput.Code.Trim();

                // 1. Resolver código → clinicId
                if (!clinicCodeToId.TryGetValue(code, out var clinicId))
                {
                    return Fail(line, $"Clínica no encontrada (código {code})");
                }

                // 2. Resolver RoleName → roleId (case-insensitive, con cache por nombre).
                var roleNameKey = clinicInput.RoleName.Trim();
                if (!roleCache.TryGetValue(roleNameKey, out var roleLookup))
                {
                    roleLookup = await authRolesClient.GetRoleByNameAsync(roleNameKey, ct);
                    roleCache[roleNameKey] = roleLookup;
                }

                if (roleLookup is null || !roleLookup.IsActive)
                {
                    return Fail(line, $"Rol no encontrado o inactivo (nombre {roleNameKey})");
                }

                // 3. Agregar asignación de clínica: primera IsPrimary=true, resto false.
                clinicAssignments.Add(new ClinicAssignmentInput(
                    ClinicId: clinicId,
                    IsPrimary: ci == 0,
                    Status: "Active"));

                // 4. Agregar scoped role para pasar a InviteEmployeeCommand.
                scopedRoles.Add(new ScopedRoleAssignmentInput(
                    RoleId: roleLookup.Id,
                    ScopeType: "Clinic",
                    ScopeId: clinicId));
            }
        }

        // ── Creación del empleado ──────────────────────────────────────

        var entity = new Employee
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            FirstName = row.FirstName.Trim(),
            LastName = row.LastName.Trim(),
            Email = email,
            Status = mappedStatus,
            CreatedAt = DateTime.UtcNow,
        };

        // Si es profesional clínico, crear extensión (misma lógica que CreateEmployeeCommand).
        if (professionalTypeId is not null)
        {
            entity.Professional = new Professional
            {
                Id = Guid.NewGuid(),
                ProfessionalTypeId = professionalTypeId,
                CreatedAt = DateTime.UtcNow,
            };
        }

        // Aplicar asignaciones de clínicas (reutiliza patrón de CreateEmployeeCommand).
        if (clinicAssignments is { Count: > 0 })
        {
            var now = DateTime.UtcNow;
            entity.ClinicAssignments = clinicAssignments
                .Select(a => new EmployeeClinic
                {
                    ClinicId = a.ClinicId,
                    IsPrimary = a.IsPrimary,
                    Status = string.IsNullOrWhiteSpace(a.Status) ? "Active" : a.Status.Trim(),
                    CreatedAt = now,
                })
                .ToList();
        }

        await employeeRepository.AddAsync(entity, ct);

        logger.LogInformation(
            "Bulk create: fila {Line} → empleado {EmployeeId} creado ({Email}, profesional: {IsProfessional}, clínicas: {ClinicCount}).",
            line, entity.Id, entity.Email, entity.Professional is not null,
            clinicAssignments?.Count ?? 0);

        // ── Auto-invite: crear usuario en Auth + enviar enlace ────────
        try
        {
            var inviteResult = await mediator.Send(
                new InviteEmployeeCommand(
                    entity.Id,
                    InvitedBy: null,
                    ScopedRoles: scopedRoles is { Count: > 0 } ? scopedRoles : null),
                ct);

            logger.LogInformation(
                "Bulk create: fila {Line} → invitación enviada (usuario {UserId}).",
                line, inviteResult.UserId);

            return new BulkRowResultDto(line, true, EmployeeId: entity.Id, UserId: inviteResult.UserId);
        }
        catch (Exception ex)
        {
            // Compensación: si la invitación falla, eliminar el empleado recién
            // creado para no dejar estados a medias (patrón de CreateProfessionalCommand).
            logger.LogError(ex,
                "Bulk create: fila {Line} → invitación falló, compensando (eliminando empleado {EmployeeId}).",
                line, entity.Id);

            try
            {
                await employeeRepository.DeleteAsync(entity.Id, ct);
            }
            catch (Exception deleteEx)
            {
                logger.LogError(deleteEx,
                    "Bulk create: fila {Line} → no se pudo eliminar el empleado {EmployeeId} durante la compensación.",
                    line, entity.Id);
            }

            var reason = ex.Message.Contains("invitación", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("usuario", StringComparison.OrdinalIgnoreCase)
                    ? ex.Message
                    : $"La invitación falló: {ex.Message}";

            return Fail(line, $"No se pudo enviar la invitación de acceso: {reason}");
        }
    }

    private static BulkRowResultDto Fail(int line, string error)
        => new(line, false, Error: error);

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}
