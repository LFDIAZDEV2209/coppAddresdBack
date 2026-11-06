using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace CoppAddresd.Infrastructure.Services;

/// <summary>Un trabajo por proceso. Claim atómico SKIP LOCKED entre réplicas;
/// sin transacciones ni conexiones abiertas durante descarga/conversión/S3.</summary>
public sealed class ExerciseMediaImportWorker(IServiceScopeFactory scopes, ILogger<ExerciseMediaImportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Set<ExerciseMediaImportJob>().Where(x => x.Status == "processing" && x.LeaseUntil < DateTimeOffset.UtcNow && x.Attempts >= 3)
                    .ExecuteUpdateAsync(u => u.SetProperty(x => x.Status, "failed").SetProperty(x => x.Error, "No se pudo recuperar la importación. Inicia otra o usa la biblioteca."), stoppingToken);
                var jobs = await db.Set<ExerciseMediaImportJob>().FromSqlRaw("""
                    WITH pending AS (
                      SELECT id FROM app.exercise_media_import_jobs
                      WHERE attempts < 3 AND (status='queued' OR (status='processing' AND lease_until < now()))
                      ORDER BY created_at FOR UPDATE SKIP LOCKED LIMIT 1
                    )
                    UPDATE app.exercise_media_import_jobs j SET status='processing', attempts=attempts+1,
                      lease_until=now()+interval '6 minutes', updated_at=now()
                    FROM pending WHERE j.id=pending.id RETURNING j.*
                    """).AsNoTracking().ToListAsync(stoppingToken);
                var job = jobs.FirstOrDefault();
                if (job is null) { await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken); continue; }
                try
                {
                    var source = scope.ServiceProvider.GetRequiredService<IExerciseMediaSource>();
                    // El medio usa el ID del trabajo: un reinicio después de publicar
                    // recupera el resultado, y un PUT incompleto reutiliza sus mismas claves.
                    var previous = await db.MediaItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == job.Id, stoppingToken);
                    var item = previous is null ? await source.ImportAsync(new(job.SourceId, job.Kind, job.Id), job.UserId, stoppingToken)
                        : CoppAddresd.Application.Features.Media.MediaItemDto.FromEntity(previous);
                    await db.Set<ExerciseMediaImportJob>().Where(x => x.Id == job.Id).ExecuteUpdateAsync(u => u
                        .SetProperty(x => x.Status, "completed").SetProperty(x => x.MediaId, item.Id)
                        .SetProperty(x => x.LeaseUntil, (DateTimeOffset?)null).SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Falló la importación de ejercicio {JobId}", job.Id);
                    var message = ex is UnprocessableEntityException ? ex.Message : "No se pudo preparar la demostración. Usa otro recurso o un archivo propio.";
                    await db.Set<ExerciseMediaImportJob>().Where(x => x.Id == job.Id).ExecuteUpdateAsync(u => u
                        .SetProperty(x => x.Status, "failed").SetProperty(x => x.Error, message)
                        .SetProperty(x => x.LeaseUntil, (DateTimeOffset?)null).SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "No se pudo procesar la cola de vídeos"); await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
        }
    }
}
