using CoppAddresd.Application.Features.Nutrition.Cases;
using System.Text.Json;
using CoppAddresd.Application.DTOs.Ai;
using CoppAddresd.Application.Features.Wellness;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Enums.HealthTests;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Outbox de nutrición: la fila del plan reserva cada ciclo; una lease y la
/// revisión optimista evitan publicar dos veces desde varias réplicas.
/// Ninguna llamada a la IA se ejecuta dentro de una transacción de base de datos.
/// </summary>
public sealed class NutritionPlanWorkflow(AppDbContext db, IClinicalContextService clinical,
    ISafetyRulesService safety, IAiServiceClient ai, ILogger<NutritionPlanWorkflow> logger)
    : INutritionPlanWorkflow
{
    public async Task<bool> CanAccessPatientAsync(Guid patientId, NutritionReviewActor actor, CancellationToken ct)
        => actor.ViewAll || (actor.ProfessionalId is { } professionalId
            && await db.PatientProfessionalAssignments.AsNoTracking().AnyAsync(x =>
                x.PatientId == patientId && x.ProfessionalId == professionalId && x.Status == "Active", ct));

    private async Task<(bool Assigned, bool Ready)> ReadinessAsync(Guid patientId, CancellationToken ct)
    {
        var batteries = await db.HealthTestBatteryAssignments.AsNoTracking()
            .Where(x => x.PatientId == patientId && x.Status != HealthTestAssignmentStatus.cancelled)
            .Include(x => x.Battery!).ThenInclude(x => x.Items)
            .Include(x => x.Assignments).ThenInclude(x => x.Version)
            .AsSplitQuery().ToListAsync(ct);
        if (batteries.Count == 0) return (false, false);
        var ready = batteries.All(b =>
        {
            if (b.Battery is not null && b.Battery.Items.Count > 0)
                return b.Assignments.Count > 0 && b.Battery.Items.Where(i => i.IsRequired).All(i =>
                    b.Assignments.Any(a => a.Version?.InstrumentId == i.InstrumentId
                        && a.Status == HealthTestAssignmentStatus.completed));
            var required = b.Assignments.Where(a => b.Battery is null || b.Battery.Items.Count == 0
                || b.Battery.Items.Any(i => i.IsRequired && i.InstrumentId == a.Version!.InstrumentId)).ToList();
            return b.Assignments.Count > 0 && required.All(a => a.Status == HealthTestAssignmentStatus.completed);
        });
        return (true, ready);
    }

    public async Task QueueIfReadyAsync(Guid patientId, CancellationToken ct)
    {
        // La batería inicial pertenece al productor nuevo incluso antes de que llegue su evento.
        if (await db.NutritionDraftReservations.AsNoTracking().AnyAsync(r => r.PatientId == patientId, ct)
            || await db.HealthTestBatteryAssignments.AsNoTracking().AnyAsync(b => b.PatientId == patientId
                && b.Status != HealthTestAssignmentStatus.cancelled && b.Battery!.Code == "bateria-inicial"
                && b.Battery.IsActive && b.Battery.AutoAssignOnPatientCreate, ct)) return;
        if (!(await ReadinessAsync(patientId, ct)).Ready) return;
        var plans = db.NutritionPlans.AsNoTracking().Where(x => x.PatientId == patientId && !x.IsTemplate);
        if (await plans.AnyAsync(x => x.ReviewStatus == "pending_review" && x.Status != NutritionPlanStatus.Archived, ct)) return;
        var latest = await plans.Where(x => x.ApprovedAt != null)
            .OrderByDescending(x => x.ApprovedAt).FirstOrDefaultAsync(ct);
        if (latest is not null && await db.NutritionPlanAssignments.AsNoTracking()
            .AnyAsync(a => a.PlanId == latest.Id && a.EndDate >= DateTime.UtcNow.Date, ct)) return;
        var key = $"{patientId:N}:{(latest is null ? "initial" : latest.Id.ToString("N"))}";
        if (await plans.AnyAsync(x => x.GenerationKey == key, ct)) return;
        var plan = new NutritionPlan
        {
            Id = Guid.NewGuid(), PatientId = patientId, IsTemplate = false,
            Name = "Plan nutricional · 30 días", DurationDays = 30,
            SourcePlanId = latest?.Id, GenerationKey = key, GenerationStatus = "queued", GenerationNextAttemptAt = DateTime.UtcNow,
            ReviewStatus = "pending_review", Status = NutritionPlanStatus.Draft,
        };
        db.NutritionPlans.Add(plan);
        db.NutritionPlanReviewEvents.Add(Event(plan, "generation_queued", null));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // Otra réplica ya reservó exactamente el mismo ciclo.
            db.ChangeTracker.Clear();
        }
    }

    public async Task ReconcileAsync(CancellationToken ct)
    {
        // Publica generaciones legacy completas que quedaron esperando revisión.
        // Un borrador inválido se difiere para que no bloquee los siguientes lotes.
        var now = DateTime.UtcNow;
        var ready = await db.NutritionPlans.AsNoTracking()
            .Where(p => !p.IsTemplate && p.PatientId != null && p.GenerationStatus == "completed"
                && p.ReviewStatus == "pending_review" && p.Status != NutritionPlanStatus.Archived
                && (p.GenerationNextAttemptAt == null || p.GenerationNextAttemptAt <= now))
            .OrderBy(p => p.GenerationNextAttemptAt ?? p.CreatedAt)
            .ThenBy(p => p.Id).Select(p => new { p.Id, p.Revision }).Take(25).ToListAsync(ct);
        foreach (var item in ready)
        {
            using var actorScope = NutritionSystemActor.Begin();
            try
            {
                await ApproveAsync([new(item.Id, item.Revision, "Publicación automática del programa; sin aprobación clínica previa.")],
                    new(NutritionSystemActor.Id, null, true), ct);
            }
            catch (Exception ex) when (ex is UnprocessableEntityException or BusinessRuleViolationException)
            {
                var retryAt = now.AddMinutes(15);
                await db.NutritionPlans.Where(p => p.Id == item.Id && p.Revision == item.Revision
                    && p.GenerationStatus == "completed" && p.ReviewStatus == "pending_review")
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.GenerationNextAttemptAt, retryAt), ct);
                logger.LogWarning("Nutrition.AutoPublicationDeferred PlanId={PlanId} FailureType={FailureType}", item.Id, ex.GetType().Name);
            }
        }

        // Recupera el hook perdido por un reinicio y renueva ciclos vencidos.
        // El índice patient_id de las asignaciones y el de planes mantienen el batch acotado.
        var ids = await db.HealthTestBatteryAssignments.AsNoTracking()
            .Where(b => !db.NutritionDraftReservations.Any(r => r.PatientId == b.PatientId)
                && !db.HealthTestBatteryAssignments.Any(initial => initial.PatientId == b.PatientId
                    && initial.Status != HealthTestAssignmentStatus.cancelled && initial.Battery!.Code == "bateria-inicial"
                    && initial.Battery.IsActive && initial.Battery.AutoAssignOnPatientCreate)
                && b.Status != HealthTestAssignmentStatus.cancelled
                && !db.HealthTestBatteryAssignments.Any(other => other.PatientId == b.PatientId
                    && other.Status != HealthTestAssignmentStatus.cancelled
                    && (!other.Assignments.Any()
                        || other.Battery!.Items.Any() && other.Battery.Items.Any(i => i.IsRequired
                            && !other.Assignments.Any(a => a.Version!.InstrumentId == i.InstrumentId && a.Status == HealthTestAssignmentStatus.completed))
                        || !other.Battery.Items.Any() && other.Assignments.Any(a => a.Status != HealthTestAssignmentStatus.completed)))
                && !db.NutritionPlans.Any(p => p.PatientId == b.PatientId && !p.IsTemplate && p.ReviewStatus == "pending_review" && p.Status != NutritionPlanStatus.Archived)
                && !db.NutritionPlans.Any(reservation => reservation.PatientId == b.PatientId
                    && reservation.GenerationKey != null && reservation.ApprovedAt == null
                    && reservation.SourcePlanId == db.NutritionPlans.Where(previous => previous.PatientId == b.PatientId
                        && previous.ApprovedAt != null).OrderByDescending(previous => previous.ApprovedAt)
                        .Select(previous => (Guid?)previous.Id).FirstOrDefault())
                && !db.NutritionPlanAssignments.Any(a => a.PatientId == b.PatientId
                    && a.Status == AssignmentStatus.Active && a.EndDate >= DateTime.UtcNow.Date
                    && a.Plan != null && !a.Plan.IsTemplate && a.Plan.PatientId == b.PatientId
                    && a.Plan.ReviewStatus == "approved" && a.Plan.Status == NutritionPlanStatus.Active))
            .Select(b => b.PatientId).Distinct().OrderBy(id => id).Take(25).ToListAsync(ct);
        foreach (var id in ids) await QueueIfReadyAsync(id, ct);
    }

    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var plan = await db.NutritionPlans.Where(p => p.Status != NutritionPlanStatus.Archived
            && !db.NutritionDraftReservations.Any(r => r.PatientId == p.PatientId)
            && !db.HealthTestBatteryAssignments.Any(initial => initial.PatientId == p.PatientId
                && initial.Status != HealthTestAssignmentStatus.cancelled && initial.Battery!.Code == "bateria-inicial"
                && initial.Battery.IsActive && initial.Battery.AutoAssignOnPatientCreate)).Where(p =>
                p.GenerationAttempts < 5 && (p.GenerationStatus == "queued" || p.GenerationStatus == "failed") && p.GenerationNextAttemptAt <= now
                    || p.GenerationStatus == "generating" && p.GenerationLeaseUntil < now)
            .OrderBy(p => p.GenerationNextAttemptAt).ThenBy(p => p.Id).FirstOrDefaultAsync(ct);
        if (plan is null) return false;
        if (plan.GenerationAttempts >= 5)
        {
            // Un reinicio durante el último intento no deja una lease eterna.
            // El revisor puede completar/editar el borrador; nunca se publica IA parcial.
            plan.GenerationStatus = "failed";
            plan.GenerationLeaseUntil = null;
            plan.GenerationNextAttemptAt = null;
            plan.Revision++;
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); }
            return true;
        }
        plan.GenerationStatus = "generating";
        plan.GenerationLeaseUntil = now.AddMinutes(5);
        plan.GenerationAttempts++;
        plan.Revision++;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return true; }
        var claimedRevision = plan.Revision;
        var claimedId = plan.Id;
        var claimedAttempt = plan.GenerationAttempts;
        try
        {
            if (plan.PatientId is not { } patientId || !(await ReadinessAsync(patientId, ct)).Ready)
                throw new InvalidOperationException("Las baterías requeridas aún no están completas.");
            var assessments = await db.HealthTestResults.AsNoTracking()
                .Where(r => r.Evaluation!.PatientId == patientId
                    && r.Evaluation.Status == HealthTestEvaluationStatus.completed)
                .OrderByDescending(r => r.CreatedAt).Take(250)
                .Select(r => new ClinicalAssessmentDto(r.Code, r.Label, r.Value, r.Qualifier,
                    r.Severity.HasValue ? r.Severity.Value.ToString() : null, r.CreatedAt))
                .ToListAsync(ct);
            assessments = assessments.GroupBy(a => a.Code).Select(g => g.First()).ToList();
            var context = (await clinical.ConsolidateAsync(patientId, ct)) with { Assessments = assessments };
            var restrictions = await safety.EvaluateAsync(context, ct);
            using var attempt = CancellationTokenSource.CreateLinkedTokenSource(ct);
            attempt.CancelAfter(TimeSpan.FromMinutes(4));
            var generated = await ai.GeneratePlanAsync("nutrition",
                context with { SubjectPatientId = patientId }, restrictions, attempt.Token);
            ApplyGeneratedPlan(plan, generated.Plan);
            // Las PK UUID ya tienen valor; marcar explícitamente Added evita
            // que EF interprete la nueva colección como filas existentes.
            db.NutritionPlanDays.AddRange(plan.Days);
            plan.AssessmentSnapshot = JsonSerializer.Serialize(assessments);
            plan.GenerationStatus = "completed";
            plan.GenerationLeaseUntil = null;
            plan.GenerationNextAttemptAt = null;
            plan.UpdatedAt = DateTime.UtcNow;
            plan.Revision++;
            db.NutritionPlanReviewEvents.Add(Event(plan, "generated", null));
            await db.SaveChangesAsync(ct);
            using var actorScope = NutritionSystemActor.Begin();
            await ApproveAsync([new(plan.Id,plan.Revision,"Publicación automática del programa; sin aprobación clínica previa.")],new(NutritionSystemActor.Id,null,true),ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return true;
        }
        catch (Exception ex)
        {
            // Ni el contenido clínico ni el mensaje del proveedor salen a los logs.
            logger.LogWarning("Generación nutricional pendiente de reintento: plan={PlanId} tipo={ExceptionType}", plan.Id, ex.GetType().Name);
            // Descartar días/escalas inválidos antes de registrar el fallo. Una
            // escritura fallida no debe reintentarse con el mismo grafo sucio.
            db.ChangeTracker.Clear();
            var failed = await db.NutritionPlans.SingleOrDefaultAsync(p => p.Id == claimedId
                && p.Revision == claimedRevision && p.GenerationAttempts == claimedAttempt
                && p.GenerationStatus == "generating", ct);
            if (failed is null) return true;
            failed.GenerationStatus = "failed";
            failed.GenerationLeaseUntil = null;
            failed.GenerationNextAttemptAt = DateTime.UtcNow.AddMinutes(Math.Pow(2, claimedAttempt));
            failed.Revision++;
            await db.SaveChangesAsync(ct);
        }
        return true;
    }

    public async Task<NutritionPlanStatusDto> GetStatusAsync(Guid userId, string? timezone, CancellationToken ct)
    {
        var patient = await db.PatientProfiles.AsNoTracking().Where(p => p.UserId == userId)
            .Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("No existe perfil de paciente.");
        timezone ??= await db.ProgramEnrollments.AsNoTracking().Where(e => e.PatientId == patient)
            .OrderByDescending(e => e.CreatedAt).Select(e => e.Timezone).FirstOrDefaultAsync(ct);
        var today = LocalToday(timezone);
        var current = await db.NutritionPrescriptions.AsNoTracking().Where(p=>p.PatientId==patient && p.State=="aprobado" && p.IsPublished
            && p.StartDate<=today && p.EndDate>=today).OrderByDescending(p=>p.StartDate).ThenByDescending(p=>p.Version)
            .Select(p=>new {p.Id,p.StartDate,p.EndDate,p.Revision}).FirstOrDefaultAsync(ct);
        if(current is not null)return new("active",30,current.StartDate,current.EndDate,today.DayNumber-current.StartDate!.Value.DayNumber+1,today,current.Id,current.Revision);
        var generation=await db.NutritionDraftReservations.AsNoTracking().Where(r=>r.PatientId==patient)
            .OrderByDescending(r=>r.CreatedAt).ThenByDescending(r=>r.Id).Select(r=>new {r.State}).FirstOrDefaultAsync(ct);
        // Una reserva lista de un ciclo vencido no significa que siga generándose.
        // Tampoco debe ocultar una asignación legacy vigente.

        var active = await db.NutritionPlanAssignments.AsNoTracking()
            .Where(a => a.PatientId == patient && a.Status == AssignmentStatus.Active
                && a.Plan.PatientId == patient && !a.Plan.IsTemplate
                && a.Plan.ApprovedAt != null && a.Plan.ApprovedBy != null && a.Plan.ReviewStatus == "approved"
                && a.Plan.Status == NutritionPlanStatus.Active
                && a.StartDate >= today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(-29)
                && a.StartDate < today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(1)
                && a.EndDate >= today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
            .OrderByDescending(a => a.StartDate)
            .Select(a => new { a.PlanId, a.StartDate, a.EndDate, a.Plan.Revision }).FirstOrDefaultAsync(ct);
        if (active is not null)
        {
            var start = DateOnly.FromDateTime(active.StartDate);
            return new("active", 30, start, DateOnly.FromDateTime(active.EndDate!.Value), today.DayNumber - start.DayNumber + 1, today, active.PlanId, active.Revision);
        }
        if (generation is not null && generation.State != "ready")
            return new(generation.State == "failed" ? "generation_failed" : "generating", TodayLocalDate: today);
        if (generation?.State == "ready")
            return new("expired", TodayLocalDate: today);
        var plan = await db.NutritionPlans.AsNoTracking().Where(p => p.PatientId == patient && !p.IsTemplate)
            .OrderByDescending(p => p.CreatedAt).FirstOrDefaultAsync(ct);
        if (plan is not null && plan.Status != NutritionPlanStatus.Archived)
        {
            var status = plan.GenerationStatus switch
            {
                "queued" or "generating" => "generating",
                "failed" => "generation_failed",
                _ when plan.ReviewStatus == "pending_review" => "pending_review",
                _ => "expired",
            };
            return new(status, TodayLocalDate: today, Revision: plan.Revision);
        }
        var readiness = await ReadinessAsync(patient, ct);
        return new(readiness.Ready ? "no_plan" : readiness.Assigned ? "assessments_pending" : "no_assessment_assignment", TodayLocalDate: today);
    }

    public async Task<IReadOnlyList<NutritionPlanDto>> ApproveAsync(IReadOnlyList<NutritionPlanApprovalInput> inputs,
        NutritionReviewActor actor, CancellationToken ct)
    {
        if (inputs is null || inputs.Count is < 1 or > 100 || inputs.Select(x => x.Id).Distinct().Count() != inputs.Count)
            throw new UnprocessableEntityException("Selecciona entre 1 y 100 planes distintos.");
        var ids = inputs.Select(x => x.Id).ToArray();
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            // Releer todas las entidades en cada reintento; nunca reutilizar
            // estados mutados de una transacción abortada.
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var owners = await db.NutritionPlans.AsNoTracking().Where(p => ids.Contains(p.Id))
                .Select(p => p.PatientId).ToListAsync(ct);
            if (owners.Count != inputs.Count || owners.Any(id => id is null)) throw new NotFoundException("No se encontraron los planes seleccionados.");
            var patientIds = owners.Select(id => id!.Value).Distinct().OrderBy(id => id).ToArray();
            if (patientIds.Length != inputs.Count) throw new UnprocessableEntityException("Aprueba un solo plan por paciente en cada lote.");
            if (!actor.ViewAll && (actor.ProfessionalId is not { } professionalId
                || await db.PatientProfessionalAssignments.AsNoTracking()
                    .Where(a => patientIds.Contains(a.PatientId) && a.ProfessionalId == professionalId && a.Status == "Active")
                    .Select(a => a.PatientId).Distinct().CountAsync(ct) != patientIds.Length))
                throw new NotFoundException("No se encontraron los planes seleccionados.");
            // Orden estable evita deadlocks. Las FK de nuevas baterías toman
            // key-share y esperan hasta finalizar esta publicación.
            await db.PatientProfiles.FromSqlInterpolated($"SELECT * FROM app.patient_profiles WHERE id = ANY ({patientIds}) ORDER BY id FOR UPDATE").ToListAsync(ct);
            var plans = await db.NutritionPlans.Include(p => p.Days).Where(p => ids.Contains(p.Id)).ToListAsync(ct);
            var batteries = await db.HealthTestBatteryAssignments.AsNoTracking()
                .Where(b => patientIds.Contains(b.PatientId) && b.Status != HealthTestAssignmentStatus.cancelled)
                .Include(b => b.Battery!).ThenInclude(b => b.Items)
                .Include(b => b.Assignments).ThenInclude(a => a.Version).AsSplitQuery().ToListAsync(ct);
            foreach (var plan in plans)
            {
                if (plan.Revision != inputs.First(i => i.Id == plan.Id).Revision)
                    throw new BusinessRuleViolationException("El plan cambió. Actualiza la página y revísalo de nuevo.");
                var ownBatteries = batteries.Where(b => b.PatientId == plan.PatientId).ToList();
                if (ownBatteries.Count == 0 || ownBatteries.Any(b => b.Assignments.Count == 0
                    || b.Battery is { Items.Count: > 0 } && b.Battery.Items.Where(i => i.IsRequired)
                        .Any(i => !b.Assignments.Any(a => a.Version?.InstrumentId == i.InstrumentId && a.Status == HealthTestAssignmentStatus.completed))
                    || (b.Battery is null || b.Battery.Items.Count == 0) && b.Assignments.Any(a => a.Status != HealthTestAssignmentStatus.completed)))
                    throw new UnprocessableEntityException("El paciente debe completar todas sus baterías requeridas.");
                NutritionPlanReviewRules.ValidateForApproval(plan);
            }
            var timezones = (await db.ProgramEnrollments.AsNoTracking().Where(e => patientIds.Contains(e.PatientId))
                .OrderByDescending(e => e.CreatedAt).Select(e => new { e.PatientId, e.Timezone }).ToListAsync(ct))
                .GroupBy(e => e.PatientId).ToDictionary(g => g.Key, g => g.First().Timezone);
            var old = await db.NutritionPlanAssignments.Include(a => a.Plan)
                .Where(a => patientIds.Contains(a.PatientId) && a.Status == AssignmentStatus.Active).ToListAsync(ct);
            if (old.Any(a => !ids.Contains(a.PlanId)
                && a.Plan is { IsTemplate: false, ReviewStatus: "approved", Status: NutritionPlanStatus.Active }
                && a.Plan.PatientId == a.PatientId && (a.EndDate is null
                || DateOnly.FromDateTime(a.EndDate.Value) >= LocalToday(timezones.GetValueOrDefault(a.PatientId)))))
                throw new BusinessRuleViolationException("El paciente ya tiene un ciclo vigente. Para ajustarlo, modifica y vuelve a aprobar su plan actual.");
            foreach (var assignment in old) { assignment.Status = AssignmentStatus.Completed; assignment.UpdatedAt = DateTime.UtcNow; }
            foreach (var plan in plans)
            {
                plan.ReviewStatus = "approved"; plan.Status = NutritionPlanStatus.Active;
                plan.ApprovedAt = DateTime.UtcNow; plan.ApprovedBy = actor.UserId; plan.UpdatedBy = actor.UserId; plan.Revision++;
                var today = LocalToday(timezones.GetValueOrDefault(plan.PatientId!.Value));
                var continuing = old.Where(a => a.PlanId == plan.Id && a.EndDate.HasValue
                    && DateOnly.FromDateTime(a.EndDate.Value) >= today).OrderByDescending(a => a.StartDate).FirstOrDefault();
                var start = continuing?.StartDate ?? today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                db.NutritionPlanAssignments.Add(new NutritionPlanAssignment
                {
                    Id = Guid.NewGuid(), PatientId = plan.PatientId.Value, PlanId = plan.Id,
                    StartDate = start, EndDate = start.AddDays(29), CreatedBy = actor.UserId, Status = AssignmentStatus.Active,
                });
                db.NutritionPlanReviewEvents.Add(Event(plan, "approved", actor.UserId, inputs.First(i => i.Id == plan.Id).Notes));
            }
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { throw new BusinessRuleViolationException("Un plan cambió durante la revisión. Ningún plan de este lote fue aprobado."); }
            await transaction.CommitAsync(ct);
            return plans.Select(p => NutritionPlanDto.FromEntity(p)).ToList();
        });
    }

    public async Task<IReadOnlyList<NutritionPlanReviewEventDto>> HistoryAsync(Guid planId, NutritionReviewActor actor, CancellationToken ct)
    {
        var plan = await db.NutritionPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan?.PatientId is not { } patientId || !await CanAccessPatientAsync(patientId, actor, ct)) throw new NotFoundException("Plan no encontrado.");
        var events = db.NutritionPlanReviewEvents.AsNoTracking().Where(e => e.PlanId == planId)
            .OrderByDescending(e => e.OccurredAt).Take(200);
        return await (from e in events
            join employee in db.Employees.AsNoTracking() on e.ActorUserId equals employee.UserId into actors
            from actorEmployee in actors.DefaultIfEmpty()
            orderby e.OccurredAt descending
            select new NutritionPlanReviewEventDto(e.Id, e.ActorUserId, e.OccurredAt, e.Action, e.Revision,
                e.Notes, actorEmployee != null ? actorEmployee.FirstName + " " + actorEmployee.LastName : null)).ToListAsync(ct);
    }

    public static DateOnly LocalToday(string? timezone)
    {
        try { return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(timezone ?? "UTC"))); }
        catch (TimeZoneNotFoundException) { return DateOnly.FromDateTime(DateTime.UtcNow); }
        catch (InvalidTimeZoneException) { return DateOnly.FromDateTime(DateTime.UtcNow); }
    }

    public static NutritionPlanReviewEvent Event(NutritionPlan plan, string action, Guid? actor, string? notes = null) => new()
    {
        Id = Guid.NewGuid(), PlanId = plan.Id, Action = action, ActorUserId = actor,
        OccurredAt = DateTime.UtcNow, Revision = plan.Revision, Notes = notes?.Trim()[..Math.Min(notes.Trim().Length, 1000)],
        Snapshot = JsonSerializer.Serialize(new { plan.Name, plan.Description, plan.DurationDays, plan.DailyCalorieTarget,
            plan.DailyProteinTarget, plan.DailyCarbsTarget, plan.DailyFatTarget, plan.DailyFiberTarget,
            plan.Allergens, plan.MealTiming, plan.ReviewStatus, plan.ApprovedBy, plan.ApprovedAt,
            Days = plan.Days.Select(d => new { d.DayNumber, d.MealType, d.Description, d.Foods, d.Calories,
                d.ProteinG, d.CarbsG, d.FatG, d.FiberG, d.WaterMl, d.DailyWaterMl, d.Notes, d.SortOrder }) }),
    };

    public static void ApplyGeneratedPlan(NutritionPlan plan, JsonElement payload)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true };
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        // El servicio IA llama Merienda al enum de dominio Snack.
        var json = payload.GetRawText().Replace("\"Merienda\"", "\"Snack\"", StringComparison.OrdinalIgnoreCase);
        var input = JsonSerializer.Deserialize<CreateNutritionPlanRequest>(json, options)
            ?? throw new InvalidOperationException("La IA no devolvió un plan válido.");
        var meals = (input.Days ?? []).Select((d, index) => new NutritionPlanDay
        {
            Id = Guid.NewGuid(), PlanId = plan.Id, DayNumber = d.DayNumber, MealType = d.MealType,
            Description = d.Description, Foods = d.Foods, Calories = d.Calories,
            ProteinG = d.ProteinG, CarbsG = d.CarbsG, FatG = d.FatG, FiberG = d.FiberG,
            WaterMl = d.WaterMl, DailyWaterMl = d.DailyWaterMl > 0 ? d.DailyWaterMl : 2000,
            Notes = d.Notes, SortOrder = index + 1,
        }).ToList();
        NutritionPlanReviewRules.ValidateMenu(new NutritionPlan { Name = input.Name, DurationDays = 30, Days = meals });
        plan.Name = input.Name;
        plan.Description = input.Description;
        plan.TargetCondition = input.TargetCondition;
        plan.DurationDays = 30;
        plan.DailyCalorieTarget = input.DailyCalorieTarget;
        plan.DailyProteinTarget = input.DailyProteinTarget;
        plan.DailyCarbsTarget = input.DailyCarbsTarget;
        plan.DailyFatTarget = input.DailyFatTarget;
        plan.DailyFiberTarget = input.DailyFiberTarget;
        plan.Allergens = input.Allergens;
        plan.MealTiming = input.MealTiming;
        plan.Days = meals;
        NutritionPlanReviewRules.ValidateMenu(plan);
    }
}
