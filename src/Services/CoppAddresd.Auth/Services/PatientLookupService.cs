using CoppAddresd.Auth.Entities;
using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Auth.Services;

public class PatientLookupService : IPatientLookupService
{
    private readonly AuthDbContext _dbContext;
    private readonly ILogger<PatientLookupService> _logger;

    public PatientLookupService(AuthDbContext dbContext, ILogger<PatientLookupService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PatientLookupResult?> FindByDocumentNumberAsync(
        string documentNumber,
        CancellationToken ct = default)
    {
        var normalized = documentNumber.Trim();

        if (normalized.Length == 0)
        {
            return null;
        }

        // SQL crudo (no mapeado al modelo EF): el Auth Service lee
        // app.patient_profiles sin reclamar propiedad del esquema. La
        // comparación con LOWER() tolera diferencias de mayúsculas/minúsculas
        // en identificadores alfanuméricos (pasaportes, etc.).
        const string sql = """
            SELECT
                pp.id            AS "Id",
                pp.user_id       AS "UserId",
                pp.first_name    AS "FirstName",
                pp.last_name     AS "LastName",
                pp.document_number AS "DocumentNumber",
                pp.email         AS "Email",
                pp.phone_country_code AS "PhoneCountryCode",
                pp.phone_number  AS "PhoneNumber"
            FROM app.patient_profiles AS pp
            WHERE LOWER(pp.document_number) = LOWER({0})
              AND pp.document_number IS NOT NULL
              AND pp.document_number <> ''
            ORDER BY pp.created_at
            LIMIT 1
            """;

        var row = await _dbContext.Database
            .SqlQueryRaw<PatientLookupRow>(sql, normalized)
            .FirstOrDefaultAsync(ct);

        if (row is null)
        {
            _logger.LogInformation("Patient lookup: no patient found for document");
            return null;
        }

        return new PatientLookupResult(
            row.Id,
            row.UserId,
            row.FirstName,
            row.LastName,
            row.DocumentNumber,
            row.Email,
            row.PhoneCountryCode,
            row.PhoneNumber);
    }
}
