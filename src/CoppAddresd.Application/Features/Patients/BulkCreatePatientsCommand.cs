using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Patients;

// ─── DTOs de entrada ──────────────────────────────────────────────────

/// <summary>Fila individual del CSV importado.</summary>
public record BulkPatientRowInput(
    string FirstName,
    string LastName,
    string? DocumentNumber,
    string? Email,
    string? Status,
    string? ClinicCode = null);

/// <summary>Envelope de la solicitud de creación masiva.</summary>
public record BulkCreatePatientsRequest(
    Guid? ClinicId,
    IReadOnlyList<BulkPatientRowInput> Rows);

// ─── DTOs de salida ───────────────────────────────────────────────────

/// <summary>Resultado de una fila individual.</summary>
public record BulkPatientRowResultDto(
    int Line,
    bool Success,
    Guid? PatientId = null,
    string? Error = null);

/// <summary>Resultado global de la operación masiva.</summary>
public record BulkCreatePatientsResultDto(
    IReadOnlyList<BulkPatientRowResultDto> Results,
    int Created,
    int Failed);

// ─── Comando MediatR ─────────────────────────────────────────────────

public record BulkCreatePatientsCommand(
    Guid? ClinicId,
    IReadOnlyList<BulkPatientRowInput> Rows,
    Guid? CreatedBy,
    Guid? CreatedByProfessionalId) : IRequest<BulkCreatePatientsResultDto>;

// ─── Validador FluentValidation (envelope) ───────────────────────────

public sealed class BulkCreatePatientsValidator : AbstractValidator<BulkCreatePatientsCommand>
{
    private const int MaxRows = 500;

    public BulkCreatePatientsValidator()
    {
        RuleFor(x => x.Rows)
            .NotEmpty().WithMessage("La lista de filas no puede estar vacía.")
            .Must(rows => rows.Count <= MaxRows)
            .WithMessage($"El lote no puede superar {MaxRows} filas.");
    }
}

// ─── Handler ──────────────────────────────────────────────────────────

public sealed class BulkCreatePatientsCommandHandler(
    IPatientRepository repository,
    IOrganizationRepository organizationRepository,
    ILogger<BulkCreatePatientsCommandHandler> logger)
    : IRequestHandler<BulkCreatePatientsCommand, BulkCreatePatientsResultDto>
{
    private static readonly Dictionary<string, string> StatusMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["activo"] = "Activo",
        ["inactivo"] = "Inactivo",
    };

    public async Task<BulkCreatePatientsResultDto> Handle(
        BulkCreatePatientsCommand request,
        CancellationToken ct)
    {
        // 1. Recopilar documentos del batch para buscar duplicados en BD (una sola query).
        var docsInBatch = request.Rows
            .Where(r => !string.IsNullOrWhiteSpace(r.DocumentNumber))
            .Select(r => r.DocumentNumber!.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        HashSet<string> docsInDb = [];
        if (docsInBatch.Count > 0)
        {
            var existingDocs = await repository.GetExistingDocumentNumbersAsync(docsInBatch, ct);
            docsInDb = new HashSet<string>(existingDocs, StringComparer.OrdinalIgnoreCase);
        }

        // 2. Precargar códigos de clínica del batch para resolver en batch.
        //    Se usa un diccionario para cache: código → clinicId resuelto (o null si no encontrado).
        var allClinicCodes = request.Rows
            .Where(r => !string.IsNullOrWhiteSpace(r.ClinicCode))
            .Select(r => r.ClinicCode!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Diccionario: código normalizado → Guid? (null = no encontrado, para distinguir de "no se consultó").
        Dictionary<string, Guid?> clinicCodeToId = new(StringComparer.OrdinalIgnoreCase);
        if (allClinicCodes.Count > 0)
        {
            // Buscar clínicas activas por código (query global, no por organización
            // ya que patients/bulk no tiene organizationId en el envelope).
            // Se reutiliza GetClinicsByCodesAsync con Guid.Empty como placeholder;
            // se necesita un método más general. Usamos búsqueda directa.
            foreach (var code in allClinicCodes)
            {
                // No hay método global en IOrganizationRepository; resolvemos
                // individualmente con la nueva query.
                var resolved = await ResolveClinicByCodeAsync(code, ct);
                clinicCodeToId[code] = resolved;
            }
        }

        // 3. Acumular documentos del batch para detectar duplicados internos.
        var docsInBatchTracker = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<BulkPatientRowResultDto>();

        // 4. Procesar cada fila de forma independiente.
        for (var i = 0; i < request.Rows.Count; i++)
        {
            var line = i + 1; // 1-based
            var row = request.Rows[i];

            var rowResult = await ProcessRowAsync(
                row, line, request, clinicCodeToId, docsInBatchTracker, docsInDb, ct);

            results.Add(rowResult);
        }

        var created = results.Count(r => r.Success);
        var failed = results.Count(r => !r.Success);

        logger.LogInformation(
            "Bulk create pacientes completado: {Created} creados, {Failed} fallidos de {Total} filas.",
            created, failed, request.Rows.Count);

        return new BulkCreatePatientsResultDto(results, created, failed);
    }

    /// <summary>
    /// Resuelve un código de clínica a su Id buscando clínicas activas
    /// con ese código (case-insensitive). Devuelve null si no se encontró.
    /// </summary>
    private async Task<Guid?> ResolveClinicByCodeAsync(string code, CancellationToken ct)
    {
        try
        {
            var clinics = await organizationRepository.GetClinicsByCodeAsync(code, ct);
            return clinics.Count == 1 ? clinics[0].Id : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo resolver clínica por código: {Code}", code);
            return null;
        }
    }

    private async Task<BulkPatientRowResultDto> ProcessRowAsync(
        BulkPatientRowInput row,
        int line,
        BulkCreatePatientsCommand request,
        Dictionary<string, Guid?> clinicCodeToId,
        HashSet<string> docsInBatch,
        HashSet<string> docsInDb,
        CancellationToken ct)
    {
        // ── Validaciones por fila ──────────────────────────────────────

        // firstName requerido
        if (string.IsNullOrWhiteSpace(row.FirstName))
            return Fail(line, "El nombre es requerido.");

        // lastName requerido
        if (string.IsNullOrWhiteSpace(row.LastName))
            return Fail(line, "Los apellidos son requeridos.");

        // firstName/lastName max 100
        if (row.FirstName.Trim().Length > 100)
            return Fail(line, "El nombre no puede superar los 100 caracteres.");

        if (row.LastName.Trim().Length > 100)
            return Fail(line, "Los apellidos no pueden superar los 100 caracteres.");

        // email formato válido (si está presente)
        string? email = null;
        if (!string.IsNullOrWhiteSpace(row.Email))
        {
            email = row.Email.Trim().ToLowerInvariant();
            if (!IsValidEmail(email))
                return Fail(line, "El correo no tiene un formato válido.");

            if (email.Length > 320)
                return Fail(line, "El correo no puede superar los 320 caracteres.");
        }

        // documentNumber max 50 (opcional)
        string? documentNumber = null;
        if (!string.IsNullOrWhiteSpace(row.DocumentNumber))
        {
            documentNumber = row.DocumentNumber.Trim();
            if (documentNumber.Length > 50)
                return Fail(line, "El número de documento no puede superar los 50 caracteres.");

            var docLower = documentNumber.ToLowerInvariant();

            // Duplicado dentro del batch
            if (!docsInBatch.Add(docLower))
                return Fail(line, $"El documento '{documentNumber}' está duplicado en el lote.");

            // Duplicado en BD
            if (docsInDb.Contains(docLower))
                return Fail(line, "El documento ya está registrado.");
        }

        // ── Resolver ClinicCode → clinicId (por fila) ─────────────────
        // Si la fila trae ClinicCode, se usa ese y se ignora request.ClinicId.
        // Si no trae ClinicCode, se usa el fallback: request.ClinicId ?? context.ActiveClinicId.
        Guid? effectiveClinicId = request.ClinicId;

        if (!string.IsNullOrWhiteSpace(row.ClinicCode))
        {
            var code = row.ClinicCode.Trim();
            if (!clinicCodeToId.TryGetValue(code, out var resolvedId))
            {
                // Código no estaba en el pre-cálculo (no debería pasar, pero defensiva).
                return Fail(line, $"Clínica no encontrada (código {code})");
            }

            if (resolvedId is null)
            {
                return Fail(line, $"Clínica no encontrada (código {code})");
            }

            effectiveClinicId = resolvedId;
        }

        // status vocabulario
        var status = NormalizeStatus(row.Status);

        // ── Creación del paciente ──────────────────────────────────────

        var medicalRecordNumber = GenerateMedicalRecordNumber();

        var entity = new PatientProfile
        {
            Id = Guid.NewGuid(),
            MedicalRecordNumber = medicalRecordNumber,
            FirstName = row.FirstName.Trim(),
            LastName = row.LastName.Trim(),
            DocumentNumber = documentNumber,
            Email = email,
            Status = status,
            ClinicId = effectiveClinicId,
            CreatedBy = request.CreatedBy,
            UpdatedBy = request.CreatedBy,
            CreatedAt = DateTime.UtcNow,
        };

        await repository.AddAsync(entity, ct);

        // Auto-asignación (regla de negocio): si el creador es un profesional
        // clínico, el paciente queda asignado a él ("mis pacientes").
        if (request.CreatedByProfessionalId is { } professionalId)
        {
            try
            {
                await repository.AssignProfessionalAsync(
                    entity.Id, professionalId, entity.ClinicId, "Assigned", request.CreatedBy, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex,
                    "No se pudo auto-asignar el paciente {Id} al profesional {ProfessionalId} en bulk",
                    entity.Id, professionalId);
            }
        }

        logger.LogInformation(
            "Bulk create: fila {Line} → paciente {PatientId} creado ({FirstName} {LastName}).",
            line, entity.Id, entity.FirstName, entity.LastName);

        return new BulkPatientRowResultDto(line, true, PatientId: entity.Id);
    }

    private static BulkPatientRowResultDto Fail(int line, string error)
        => new(line, false, Error: error);

    /// <summary>
    /// Normaliza el status: null/blank → "Activo"; case-insensitive → valor canónico.
    /// </summary>
    private static string NormalizeStatus(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "Activo";

        if (StatusMap.TryGetValue(raw.Trim(), out var mapped))
            return mapped;

        // Si el valor exacto está en la whitelist, usarlo tal cual
        if (PatientOptions.Statuses.Contains(raw.Trim(), StringComparer.OrdinalIgnoreCase))
            return PatientOptions.Statuses.First(s =>
                string.Equals(s, raw.Trim(), StringComparison.OrdinalIgnoreCase));

        // Valor no reconocido → fallback a Activo (consistente con single create)
        return "Activo";
    }

    private static string GenerateMedicalRecordNumber()
        => $"MRN-{Guid.NewGuid():N}"[..14].ToUpperInvariant();

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
