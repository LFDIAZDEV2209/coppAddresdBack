using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>Primera identificación únicamente; rechaza teléfonos compartidos por varios pacientes activos.</summary>
public sealed class PatientChannelQuery(AppDbContext db) : IPatientChannelQuery
{
    public async Task<PatientChannelIdentity?> FindActiveByPhoneAsync(string e164, CancellationToken ct)
    {
        var digits = e164.TrimStart('+');
        var matches = await db.Database.SqlQuery<PatientChannelIdentity>($"""
            SELECT id AS "PatientId", user_id AS "UserId", first_name AS "FirstName"
            FROM app.patient_profiles
            WHERE status = 'Activo' AND deleted_at IS NULL AND phone_number IS NOT NULL
              AND (CASE
                WHEN phone_number ~ '^\s*(\+|00)' THEN
                  regexp_replace(regexp_replace(phone_number, '^\s*00', ''), '[^0-9]', '', 'g')
                WHEN length(regexp_replace(phone_number, '[^0-9]', '', 'g')) > 10 THEN
                  regexp_replace(phone_number, '[^0-9]', '', 'g')
                ELSE COALESCE(NULLIF(regexp_replace(phone_country_code, '[^0-9]', '', 'g'), ''), '57')
                  || regexp_replace(phone_number, '[^0-9]', '', 'g') END) = {digits}
            LIMIT 2
            """).ToListAsync(ct);
        return matches.Count == 1 ? matches[0] : null;
    }
}
