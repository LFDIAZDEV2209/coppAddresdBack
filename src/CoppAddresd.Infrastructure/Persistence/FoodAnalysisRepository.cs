using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.FoodAi;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Persistence;

/// <summary>
/// Repositorio EF de análisis de comida (schema foodai). Idempotente por
/// AnalysisId: si el análisis ya existe, AddAsync devuelve el existente.
/// </summary>
public sealed class FoodAnalysisRepository : IFoodAnalysisRepository
{
    private readonly AppDbContext _db;

    public FoodAnalysisRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<FoodAnalysis?> GetByAnalysisIdAsync(
        Guid analysisId,
        CancellationToken ct = default)
    {
        return await _db.FoodAnalyses
            .AsNoTracking()
            .Include(a => a.Items.OrderBy(i => i.ItemIndex))
            .Include(a => a.Feedbacks.OrderBy(f => f.CreatedAt))
            .FirstOrDefaultAsync(a => a.AnalysisId == analysisId, ct);
    }

    public async Task<FoodAnalysis> AddAsync(
        FoodAnalysis analysis,
        CancellationToken ct = default)
    {
        var existing = await _db.FoodAnalyses
            .FirstOrDefaultAsync(a => a.AnalysisId == analysis.AnalysisId, ct);
        if (existing is not null)
        {
            // Idempotencia: mismo AnalysisId reutilizado → sin duplicados.
            return existing;
        }

        _db.FoodAnalyses.Add(analysis);
        await _db.SaveChangesAsync(ct);
        return analysis;
    }

    public async Task AddFeedbackAsync(
        FoodAnalysisFeedback feedback,
        CancellationToken ct = default)
    {
        _db.FoodAnalysisFeedbacks.Add(feedback);
        await _db.SaveChangesAsync(ct);
    }
}