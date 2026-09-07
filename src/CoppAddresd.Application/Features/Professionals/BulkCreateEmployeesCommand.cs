using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Professionals;

// ─── DTOs de entrada ──────────────────────────────────────────────────

/// <summary>Fila individual del CSV importado.</summary>
public record BulkEmployeeRowInput(
    string FirstName,
    string LastName,
    string Email,
    string? ProfessionalTypeName,
    string Status);

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
    }
}

// ─── Handler ──────────────────────────────────────────────────────────

public sealed class BulkCreateEmployeesCommandHandler(
    IEmployeeRepository employeeRepository,
    IOrganizationRepository organizationRepository,
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

        // 3. Acumular emails del batch para detectar duplicados internos.
        var emailsInBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<BulkRowResultDto>();

        // 4. Procesar cada fila de forma independiente.
        for (var i = 0; i < request.Rows.Count; i++)
        {
            var line = i + 1; // 1-based
            var row = request.Rows[i];

            var rowResult = await ProcessRowAsync(
                row, line, organization, typesByName, emailsInBatch, ct);

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

        await employeeRepository.AddAsync(entity, ct);

        logger.LogInformation(
            "Bulk create: fila {Line} → empleado {EmployeeId} creado ({Email}, profesional: {IsProfessional}).",
            line, entity.Id, entity.Email, entity.Professional is not null);

        return new BulkRowResultDto(line, true, EmployeeId: entity.Id);
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
