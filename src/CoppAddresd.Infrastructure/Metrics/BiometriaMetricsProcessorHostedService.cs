using CoppAddresd.Application.Features.ProgramProgress.Events;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Infrastructure.Metrics;

/// <summary>
/// Procesa los eventos de métricas biométricas clínicas en segundo plano, ejecutando
/// operaciones atómicas de Upsert en app.biometria_daily_metrics sin bloquear transacciones de usuario.
/// </summary>
public sealed class BiometriaMetricsProcessorHostedService(
    IBiometriaMetricsQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<BiometriaMetricsProcessorHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Iniciando procesador en segundo plano de métricas Biométricas Clínicas.");

        await foreach (var metricEvent in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                if (metricEvent is BiometriaMeasuredEvent measuredEvent)
                {
                    await ProcessMeasuredAsync(dbContext, measuredEvent, stoppingToken);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Error procesando evento de métrica biométrica: {@Event}", metricEvent);
            }
        }
    }

    private static async Task ProcessMeasuredAsync(
        AppDbContext dbContext,
        BiometriaMeasuredEvent e,
        CancellationToken ct)
    {
        var rows = new List<(string key, string dim, long count, decimal value)>();

        // 1. IMC: distribución OMS + promedio comunitario
        if (e.Imc is { } imc and > 0)
        {
            var imcCategory = imc switch
            {
                < 18.5m => "Bajo peso",
                < 25m => "Normal",
                < 30m => "Sobrepeso",
                < 35m => "Obesidad I",
                _ => "Obesidad II-III"
            };
            rows.Add(("imc_distribution", imcCategory, 1, imc));
            rows.Add(("community_avg", "imc", 1, imc));
        }

        // 2. Grasa Corporal: distribución por sexo + promedio comunitario
        if (e.BodyFatPct is { } grasa and > 0)
        {
            var isMale = e.Gender?.Trim().ToLowerInvariant() is "masculino" or "m" or "male";
            var grasaCategory = ClassifyGrasa(grasa, isMale);
            var sexPrefix = isMale ? "male" : "female";

            rows.Add(("grasa_distribution", $"{sexPrefix}_{grasaCategory}", 1, grasa));
            rows.Add(("community_avg", "grasa", 1, grasa));
        }

        // 3. Glucosa en Ayunas: distribución ADA + promedio comunitario
        if (e.GlucosaFasting is { } glucosa and > 0)
        {
            var glucosaCategory = glucosa switch
            {
                < 100m => "Normal",
                < 126m => "Prediabetes",
                _ => "Elevada"
            };
            rows.Add(("glucosa_distribution", glucosaCategory, 1, glucosa));
            rows.Add(("community_avg", "glucosa", 1, glucosa));
        }
        else
        {
            rows.Add(("glucosa_distribution", "Sin dato", 1, 0m));
        }

        // 4. Ciudad (Heat Map)
        if (!string.IsNullOrWhiteSpace(e.CityId))
        {
            var cityDim = e.CityId.Length > 64 ? e.CityId[..64] : e.CityId;
            rows.Add(("city_patient_count", cityDim, 1, 0m));
        }

        if (rows.Count == 0) return;

        // Construcción dinámica de consulta Upsert paramétrica
        // Cada fila k usa: @p(k*5) = metricDate, @p(k*5+1) = key, @p(k*5+2) = dim, @p(k*5+3) = count, @p(k*5+4) = value
        var valuesClauses = new List<string>();
        var parameters = new List<object>();

        for (var i = 0; i < rows.Count; i++)
        {
            var (key, dim, count, val) = rows[i];
            var pDate = i * 5;
            var pKey = (i * 5) + 1;
            var pDim = (i * 5) + 2;
            var pCnt = (i * 5) + 3;
            var pVal = (i * 5) + 4;

            valuesClauses.Add($"(@p{pDate}, @p{pKey}, @p{pDim}, @p{pCnt}, @p{pVal}, NOW())");
            parameters.Add(e.ObservedDate);
            parameters.Add(key);
            parameters.Add(dim);
            parameters.Add(count);
            parameters.Add(val);
        }

        var sqlUpsert = $"""
            INSERT INTO app.biometria_daily_metrics (metric_date, metric_key, dimension_key, total_count, total_value, last_updated_at)
            VALUES 
                {string.Join(",\n                ", valuesClauses)}
            ON CONFLICT (metric_date, metric_key, dimension_key)
            DO UPDATE SET 
                total_count = app.biometria_daily_metrics.total_count + EXCLUDED.total_count,
                total_value = app.biometria_daily_metrics.total_value + EXCLUDED.total_value,
                last_updated_at = NOW();
            """;

        await dbContext.Database.ExecuteSqlRawAsync(sqlUpsert, parameters.ToArray(), ct);
    }

    private static string ClassifyGrasa(decimal fat, bool isMale)
    {
        if (isMale)
        {
            return fat switch
            {
                <= 18m => "Óptimo",
                <= 24m => "Normal",
                <= 29m => "Alto",
                _ => "Obesidad"
            };
        }

        return fat switch
        {
            <= 23m => "Óptimo",
            <= 31m => "Normal",
            <= 37m => "Alto",
            _ => "Obesidad"
        };
    }
}
