using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>Reserva y lease con escrituras cortas; ninguna transacción permanece abierta durante la IA.</summary>
public sealed class NutritionDraftReservationStore(AppDbContext db) : INutritionDraftReservationStore
{
    public Task<NutritionDraftReservation> ReserveAsync(Guid patientId, Guid batteryAssignmentId, CancellationToken ct)
        => WriteAsync(async () =>
    {
        var id = Guid.NewGuid(); var now = DateTime.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO app.nutrition_draft_reservations
                (id, patient_id, battery_assignment_id, state, attempts, next_attempt_at, created_at, updated_at, fallback)
            VALUES ({id}, {patientId}, {batteryAssignmentId}, 'pending', 0, {now}, {now}, {now}, false)
            ON CONFLICT (patient_id, battery_assignment_id) DO NOTHING
            """, ct);
        return await db.NutritionDraftReservations.AsNoTracking().SingleAsync(x => x.PatientId == patientId && x.BatteryAssignmentId == batteryAssignmentId, ct);
    }, ct);

    public Task<NutritionDraftReservation?> ClaimAsync(DateTime now, CancellationToken ct)
        => WriteAsync(async () =>
    {
        await db.NutritionDraftReservations.Where(x => x.State == "generating" && x.LeaseUntil <= now && x.Attempts >= 5)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "failed").SetProperty(x => x.Reason, "attempts_exhausted")
                .SetProperty(x => x.LeaseUntil, (DateTime?)null).SetProperty(x => x.UpdatedAt, now), ct);
        var token = Guid.NewGuid(); var lease = now.AddMinutes(3);
        // Un UPDATE con SKIP LOCKED reserva una fila por réplica y vence leases de procesos interrumpidos.
        var rows = await db.NutritionDraftReservations.FromSqlInterpolated($"""
            UPDATE app.nutrition_draft_reservations SET state='generating', claim_token={token}, lease_until={lease},
                attempts=attempts+1, updated_at={now}
            WHERE id = (SELECT id FROM app.nutrition_draft_reservations
                WHERE attempts < 5 AND ((state IN ('pending','failed') AND next_attempt_at <= {now})
                    OR (state='generating' AND lease_until <= {now}))
                ORDER BY next_attempt_at, id FOR UPDATE SKIP LOCKED LIMIT 1)
            RETURNING *
            """).AsNoTracking().ToListAsync(ct);
        return rows.SingleOrDefault();
    }, ct);

    public async Task CompleteAsync(NutritionDraftReservation r, Guid prescriptionId, bool fallback, CancellationToken ct)
        => await WriteAsync(() => db.NutritionDraftReservations.Where(x => x.Id == r.Id && x.ClaimToken == r.ClaimToken)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "ready").SetProperty(x => x.PrescriptionId, prescriptionId)
                .SetProperty(x => x.Fallback, fallback).SetProperty(x => x.Reason, (string?)null)
                .SetProperty(x => x.LeaseUntil, (DateTime?)null).SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct), ct);

    public async Task FailAsync(NutritionDraftReservation r, string reason, DateTime now, CancellationToken ct)
        => await WriteAsync(() => db.NutritionDraftReservations.Where(x => x.Id == r.Id && x.ClaimToken == r.ClaimToken)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "failed").SetProperty(x => x.Reason, reason)
                .SetProperty(x => x.LeaseUntil, (DateTime?)null).SetProperty(x => x.NextAttemptAt, now.AddMinutes(1))
                .SetProperty(x => x.UpdatedAt, now), ct), ct);

    private async Task<T> WriteAsync<T>(Func<Task<T>> operation, CancellationToken ct)
        => await db.Database.CreateExecutionStrategy().ExecuteAsync(async strategyCt =>
        {
            // BEGIN activa el interceptor GUC; el pooling no retiene el actor técnico tras COMMIT.
            await using var transaction = await db.Database.BeginTransactionAsync(strategyCt);
            var result = await operation();
            await transaction.CommitAsync(strategyCt);
            return result;
        }, ct);

    public async Task<IReadOnlyList<(Guid PatientId, Guid BatteryId)>> MissingAsync(CancellationToken ct)
    {
        // Recupera eventos perdidos sin depender de inscripciones ni del flag de asignación.
        var rows = await db.HealthTestBatteryAssignments.AsNoTracking()
            .Where(b => b.Battery!.Code == "bateria-inicial" && b.Battery.IsActive && b.Battery.AutoAssignOnPatientCreate
                && b.Status != CoppAddresd.Domain.Enums.HealthTests.HealthTestAssignmentStatus.cancelled
                && b.Battery.Items.Any(i => i.IsRequired)
                && !b.Battery.Items.Any(i => i.IsRequired && !b.Assignments.Any(a => a.Version!.InstrumentId == i.InstrumentId
                    && (i.VersionId == null || i.VersionId == a.VersionId)
                    && a.Status == CoppAddresd.Domain.Enums.HealthTests.HealthTestAssignmentStatus.completed
                    && a.Evaluations.Any(e => e.Status == CoppAddresd.Domain.Enums.HealthTests.HealthTestEvaluationStatus.completed
                        && e.PatientId == b.PatientId && e.VersionId == a.VersionId)))
                && !db.NutritionDraftReservations.Any(r => r.PatientId == b.PatientId && r.BatteryAssignmentId == b.Id))
            .OrderBy(b => b.Id).Select(b => new { b.PatientId, b.Id }).Take(25).ToListAsync(ct);
        return rows.Select(x => (x.PatientId, x.Id)).ToArray();
    }
}
