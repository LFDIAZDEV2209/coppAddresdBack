using CoppAddresd.Application.DTOs.Ai;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>
/// Repositorio de mediciones clínicas para contexto de IA: proyección a DTO
/// en una sola query (join con métrica y unidad), ordenada por observación
/// descendente. Sin N+1.
/// </summary>
public sealed class ClinicalMeasurementRepository(AppDbContext dbContext) : IClinicalMeasurementRepository
{
    public async Task<IReadOnlyList<ClinicalMeasurementDto>> ListByPatientAsync(
        Guid patientId,
        CancellationToken ct = default)
        => await dbContext.ClinicalMeasurements
            .AsNoTracking()
            .Where(x => x.PatientId == patientId)
            .OrderByDescending(x => x.ObservedAt)
            .Select(x => new ClinicalMeasurementDto(
                x.Metric!.Code,
                x.Value,
                x.Unit!.Code,
                x.ObservedAt))
            .ToListAsync(ct);
}