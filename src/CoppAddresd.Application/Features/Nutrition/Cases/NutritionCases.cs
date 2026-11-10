using System.Text.Json;
using CoppAddresd.Application.Features.Nutrition.Engine;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;

namespace CoppAddresd.Application.Features.Nutrition.Cases;

/// <summary>Casos de uso nutricionales; la IA se invoca antes de abrir la transacción.</summary>
public sealed class NutritionCases(INutritionPrescriptionRepository repository, INutritionCaseStore store,
    INutritionDraftClient drafts, IClinicalContextService clinical, IProgramAssignmentRepository assessments, TimeProvider clock,
    IClinicalMeasurementRepository? clinicalMeasurements = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Json)!;
    private static string Write<T>(T value) => JsonSerializer.Serialize(value, Json);
    private static void Invalid(string message) => throw new UnprocessableEntityException(message);
    private static void Conflict(string message) => throw new BusinessRuleViolationException(message);
    private DateOnly Today(NutritionCaseContext context) => NutritionStateMachine.Today(clock.GetUtcNow(), context.Timezone);

    private async Task<NutritionCaseContext> AccessAsync(Guid patientId, NutritionActor actor, CancellationToken ct)
    {
        if (actor.UserId == Guid.Empty || !actor.CanManage && !actor.CanApprove) throw new ForbiddenException("Se requiere permiso de Nutrición.");
        if (!await store.CanAccessAsync(patientId, actor, ct)) throw new NotFoundException("Paciente no encontrado.");
        return await store.GetContextAsync(patientId, ct) ?? throw new NotFoundException("Paciente no encontrado.");
    }
    private async Task<NutritionPrescription> PlanAsync(Guid patientId, Guid? id, CancellationToken ct)
    {
        var plan = id is { } key ? await repository.GetByIdAsync(key, ct) : null;
        return plan?.PatientId == patientId ? plan : throw new NotFoundException("Prescripción no encontrada.");
    }
    public async Task<NutritionView> GetAsync(GetNutritionCaseQuery query, CancellationToken ct)
    {
        var context = await AccessAsync(query.PatientId, query.Actor, ct);
        return View(await PlanAsync(query.PatientId, query.Id, ct), context, await FoodsAsync(ct));
    }
    public async Task<IReadOnlyList<NutritionView>> ListAsync(ListNutritionCasesQuery query, CancellationToken ct)
    {
        var context = await AccessAsync(query.PatientId, query.Actor, ct); var engine = await FoodsAsync(ct);
        var rows = await repository.ListByPatientAsync(query.PatientId, query.Page, query.PageSize, ct);
        // La bandeja necesita solo cabeceras: no provoca una consulta de hijos por fila.
        return rows.Select(x => View(x, context, engine, includeMeals: false)).ToArray();
    }
    public async Task<IReadOnlyList<NutritionPrescriptionEvent>> HistoryAsync(NutritionHistoryQuery query, CancellationToken ct)
    {
        await AccessAsync(query.PatientId, query.Actor, ct); await PlanAsync(query.PatientId, query.Id, ct);
        return await store.HistoryAsync(query.Id, ct);
    }
    private async Task<NutritionEngine> FoodsAsync(CancellationToken ct) => new((await repository.ListFoodsAsync(ct)).ToDictionary(x => x.Id));

    public async Task<NutritionMeasuredProfile> ReadMeasuredProfileAsync(Guid patientId, NutritionCaseContext context,
        CancellationToken ct, NutritionGenerateInput? input = null, NutritionPatient? fallback = null)
    {
        var data = await clinical.ConsolidateAsync(patientId, ct);
        var measured = context.BatteryId is { } battery ? await assessments.ReadProfileAsync(patientId, battery, ct) : null;
        var real = clinicalMeasurements is null ? [] : await clinicalMeasurements.ListByPatientAsync(patientId, ct);
        return NutritionMeasuredProfiles.Resolve(context, data, measured, real, Today(context), input, fallback);
    }

    public Task<NutritionView> ReadViewAsync(NutritionPrescription plan, NutritionCaseContext context, CancellationToken ct)
        => ReadViewCoreAsync(plan, context, ct);
    private async Task<NutritionView> ReadViewCoreAsync(NutritionPrescription plan, NutritionCaseContext context, CancellationToken ct)
        => View(plan, context, await FoodsAsync(ct));

    public async Task<NutritionView> ExecuteAsync(NutritionCaseCommand command, CancellationToken ct)
    {
        var context = await AccessAsync(command.PatientId, command.Actor, ct);
        if (command.CorrelationId is { } correlation)
        {
            if (command.Actor.UserId != NutritionSystemActor.Id || !NutritionSystemActor.IsActive
                || command.Operation != NutritionOperation.Generate) throw new ForbiddenException("Correlación reservada al generador técnico.");
            context = context with { BatteryId = command.BatteryAssignmentId };
            if (await repository.GetByIdAsync(correlation, ct) is { } existing)
                return existing.PatientId == command.PatientId ? View(existing, context, await FoodsAsync(ct))
                    : throw new ForbiddenException("Correlación inválida.");
        }
        if (command.Operation is NutritionOperation.Approve or NutritionOperation.Return ? !command.Actor.CanApprove : !command.Actor.CanManage)
            throw new ForbiddenException("El actor no tiene permiso para esta acción.");
        if (command.Comment?.Length > 4000) Invalid("Comentario demasiado largo.");
        var engine = await FoodsAsync(ct);
        NutritionPrescription? generated = null;
        if (command.Operation is NutritionOperation.Generate or NutritionOperation.Regenerate)
        {
            NutritionPatient? fallback = null;
            if (command.Operation == NutritionOperation.Regenerate)
            {
                var prior = await PlanAsync(command.PatientId, command.Id, ct);
                if (command.Revision != prior.Revision) Conflict("La prescripción cambió; recarga la revisión.");
                NutritionStateMachine.Ensure(command.Operation, prior.State);
                fallback = Read<NutritionPatient>(prior.ClinicalProfileJson);
            }
            generated = await GenerateAsync(command.PatientId, command.Generate ?? new(), context, engine, ct, fallback);
        }
        return await store.ExecuteAsync(command.PatientId, async txCt =>
        {
            var currentContext = await AccessAsync(command.PatientId, command.Actor, txCt);
            NutritionPrescription plan;
            if (command.Operation == NutritionOperation.Generate)
            {
                if (command.CorrelationId is { } reservedId)
                {
                    // El lock de paciente serializa la publicación de dos workers con la misma correlación.
                    if (await repository.GetByIdAsync(reservedId, txCt) is { } existing) return View(existing, currentContext, engine);
                    generated!.Id = reservedId;
                }
                plan = generated!; plan.CreatedBy = command.Actor.UserId;
                if (!currentContext.TestsComplete) plan.Provisional = true;
                await repository.AddAsync(plan, txCt);
            }
            else
            {
                plan = await PlanAsync(command.PatientId, command.Id, txCt);
                if (!currentContext.TestsComplete) plan.Provisional = true;
                if (command.Revision != plan.Revision) Conflict("La prescripción cambió; recarga la revisión.");
                NutritionStateMachine.Ensure(command.Operation, plan.State);
                if (command.Operation == NutritionOperation.CreateVersion)
                {
                    plan = CopyVersion(plan, command.Actor.UserId, await store.NextVersionAsync(plan.CycleId, txCt));
                    await repository.AddAsync(plan, txCt);
                }
                else
                {
                    switch (command.Operation)
                    {
                        case NutritionOperation.Save:
                        case NutritionOperation.ApplyDay:
                            ApplyEdits(plan, command.Edit ?? new(), engine, command.Operation == NutritionOperation.ApplyDay);
                            break;
                        case NutritionOperation.Regenerate:
                            var keep = plan.ManualEditsConfirmed && (command.Generate?.PreserveConfirmedEdits ?? true);
                            plan.ClinicalProfileJson = generated!.ClinicalProfileJson; plan.MissingDataJson = generated.MissingDataJson;
                            plan.Provisional = generated.Provisional;
                            if (!keep)
                            {
                                SetTargets(plan, Read<NutritionTargets>(generated.TargetsJson)); plan.Meals = generated.Meals;
                                plan.Instructions = generated.Instructions; plan.InstructionsEn = generated.InstructionsEn;
                                plan.InstructionsEnSourceHash = generated.InstructionsEnSourceHash;
                                plan.ManualEditsConfirmed = false; plan.MenusOutdated = false; plan.NarrativeSource = generated.NarrativeSource;
                            }
                            plan.State = "borrador_ia";
                            break;
                        case NutritionOperation.Submit:
                            ValidateMeals(plan, engine); plan.State = "en_aprobacion";
                            break;
                        case NutritionOperation.Return:
                            if (string.IsNullOrWhiteSpace(command.Comment)) Invalid("La devolución exige comentario.");
                            plan.ReturnComment = command.Comment!.Trim(); plan.State = "devuelto";
                            break;
                        case NutritionOperation.Approve:
                            if (plan.MenusOutdated) Conflict("Regenera o confirma los menús ajustados a las metas.");
                            ValidateMeals(plan, engine);
                            var alerts = Flags(plan, engine);
                            if (!NutritionEngine.CanApprove(alerts, command.CriticalConfirmed)) Invalid("Confirma que revisaste personalmente las alertas críticas.");
                            var published = await store.PublishedAsync(plan.PatientId, txCt);
                            var previous = published.SingleOrDefault(x => x.CycleId == plan.CycleId);
                            var dates = NutritionStateMachine.Dates(Today(currentContext), command.StartDate, previous);
                            plan.StartDate = dates.Start; plan.EndDate = dates.End;
                            if (published.Any(x => x.CycleId != plan.CycleId && x.StartDate <= plan.EndDate && x.EndDate >= plan.StartDate))
                                Conflict("Existe otro ciclo publicado con vigencia solapada.");
                            foreach (var row in published.Where(x => x.CycleId == plan.CycleId))
                            {
                                var historical = await PlanAsync(plan.PatientId, row.Id, txCt); historical.IsPublished = false;
                                await repository.UpdateAsync(historical, txCt);
                                await EventAsync(historical, command with { Comment = "Publicación sustituida por nueva versión." }, engine, txCt, "publication_replaced");
                            }
                            plan.State = "aprobado"; plan.IsPublished = true;
                            plan.ApprovedBy = command.Actor.UserId; plan.ApprovedAt = clock.GetUtcNow().UtcDateTime;
                            break;
                        default: Invalid("Operación no admitida."); break;
                    }
                    plan.UpdatedBy = command.Actor.UserId; plan.UpdatedAt = clock.GetUtcNow().UtcDateTime;
                    await repository.UpdateAsync(plan, txCt);
                }
            }
            await EventAsync(plan, command, engine, txCt);
            return View(plan, currentContext, engine);
        }, ct);
    }

    private async Task<NutritionPrescription> GenerateAsync(Guid patientId, NutritionGenerateInput input,
        NutritionCaseContext context, NutritionEngine engine, CancellationToken ct, NutritionPatient? fallback = null)
    {
        var profile = await ReadMeasuredProfileAsync(patientId, context, ct, input, fallback);
        var p = profile.Patient ?? throw new UnprocessableEntityException("Completa los datos mínimos ausentes; no se inventan datos clínicos.");
        ValidatePatient(p);
        // Se conservan códigos anteriores del contrato de prescripción; workspace entrega nombres de campo legibles.
        var missing = profile.MissingData.Select(x => x switch { "movement" => "mov", "nutritionRisk" => "nutRiesgo",
            "sleepRiskFlags" => "sleepRF", "sleepLevel" => "sleepNivel", "testAdherence" => "adh",
            "cardiometabolicRisk" => "riesgoNum", "stress" => "estres", _ => x }).ToArray();
        var targets = NutritionEngine.ComputeTargets(p); var days = engine.BuildDays(p, ct);
        ValidateTargets(targets);
        var menus = days.SelectMany(day => NutritionEngine.MealOrder.Select(m => new DraftMenu($"{m}:{day.Tpl[m]}",
            NutritionMenus.Templates[m][day.Tpl[m]].Name, day.Meals[m].Select(i => new DraftFood(i.Id, i.G)).ToArray())))
            .DistinctBy(m => m.Key).ToArray();
        var request = new NutritionDraftRequest(new(targets.K, targets.C, targets.P, targets.F, targets.Fi, (int)(targets.Vasos * 250)),
            (await repository.ListFoodsAsync(ct)).Select(f => f.Id).ToArray(), menus,
            new("nutrition-engine-v1", profile.Fields.Where(x => x.Key != "birthDate").ToDictionary(x => x.Key,
                x => new DraftMeasuredField(x.Value.Value, x.Value.Unit, x.Value.Source, x.Value.SourceCode, x.Value.ObservedAt)),
                engine.AllFlags(p, targets, days).Select((a, i) => new DraftAppliedRule($"engine-{i + 1}", a.S, a.M, a.Tec)).ToArray(), missing));
        var draft = await drafts.GenerateAsync(request with { SubjectPatientId = patientId }, ct);
        var accepted = TryApplyDraft(draft, request, days, targets, engine);
        var snapshot = JsonSerializer.SerializeToNode(p, Json)!.AsObject();
        snapshot["measuredFields"] = JsonSerializer.SerializeToNode(profile.Fields, Json);
        snapshot["assessmentResults"] = JsonSerializer.SerializeToNode(profile.Results, Json);
        var plan = new NutritionPrescription { PatientId = patientId, Provisional = missing.Length != 0,
            ClinicalProfileJson = snapshot.ToJsonString(Json), MissingDataJson = Write(missing), NarrativeSource = accepted ? draft.Source : "deterministic_fallback",
            Instructions = accepted ? draft.Narratives.Es + "\n" + draft.Instructions.Es : "Propuesta determinista: revisa porciones y alertas antes de aprobar.",
            InstructionsEn = accepted ? draft.Narratives.En + "\n" + draft.Instructions.En : "Deterministic proposal: review portions and alerts before approval." };
        plan.InstructionsEnSourceHash = NutritionInstructionTranslation.PairedSourceHash(plan.Instructions, plan.InstructionsEn);
        SetTargets(plan, targets);
        plan.Meals = days.SelectMany(d => NutritionEngine.MealOrder.Select(m => ToMeal(new(d.D, m,
            accepted ? draft.Menus.Single(x => x.Key == $"{m}:{d.Tpl[m]}").Name : NutritionMenus.Templates[m][d.Tpl[m]].Name,
            d.Meals[m].Select(i => new NutritionItemEdit(i.Id, i.G)).ToArray())))).ToList();
        return plan;
    }
    private static bool TryApplyDraft(NutritionDraftResponse draft, NutritionDraftRequest request,
        NutritionDay[] days, NutritionTargets targets, NutritionEngine engine)
    {
        if (draft is null || draft.Narratives is null || draft.Instructions is null || draft.Menus is null
            || draft.Source is not ("llm" or "deterministic_fallback")
            || new[] { draft.Narratives.Es, draft.Narratives.En, draft.Instructions.Es, draft.Instructions.En }.Any(string.IsNullOrWhiteSpace)
            || new[] { draft.Narratives.Es, draft.Narratives.En, draft.Instructions.Es, draft.Instructions.En }.Any(x => x.Length > 4000)
            || draft.Menus.Count != request.Menus.Count || draft.Menus.Any(m => m is null)
            || draft.Menus.Select(x => x.Key).Distinct().Count() != request.Menus.Count
            || !draft.Menus.Select(x => x.Key).Order().SequenceEqual(request.Menus.Select(x => x.Key).Order())
            || draft.Menus.Any(m => m is null || m.Name is null || m.Name.Length is 0 or > 200 || m.Items is null || m.Items.Count is 0 or > 20
                || m.Items.Any(i => i is null || !request.CatalogIds.Contains(i.FoodId) || !double.IsFinite(i.Grams) || i.Grams <= 0 || i.Grams > 2000))) return false;
        var proposed = days.Select(d => d with { Meals = NutritionEngine.MealOrder.ToDictionary(m => m,
            m => draft.Menus.Single(x => x.Key == $"{m}:{d.Tpl[m]}").Items.Select(i => new NutritionItem(i.FoodId, i.Grams)).ToArray()) }).ToArray();
        // El fallback recibido preserva el menú original incluso si ya tiene alertas del prototipo.
        var unchanged = request.Menus.All(m => draft.Menus.Single(x => x.Key == m.Key).Items.SequenceEqual(m.Items));
        if (!unchanged && draft.Menus.Any(m => m.Items.Any(i => engine.RoundGrams(i.FoodId, i.Grams) != i.Grams))) return false;
        if (!unchanged && engine.ValidatePlan(targets, proposed).Count > 0) return false;
        foreach (var day in days) foreach (var meal in NutritionEngine.MealOrder) day.Meals[meal] = proposed[day.D - 1].Meals[meal];
        return true;
    }
    private static void ValidatePatient(NutritionPatient p)
    {
        var values = new[] { p.Peso, p.Talla, p.Edad, p.Imc, p.Hba1c, p.SleepRF, p.RiesgoNum, p.Deficit, p.Kcal, p.CarbPct, p.ProtG, p.Fiber, p.Vasos };
        if (values.Any(x => x.HasValue && !double.IsFinite(x.Value))) Invalid("No se admiten números no finitos.");
    }
    private static void ValidateTargets(NutritionTargets t)
    {
        if (new[] { t.K, t.C, t.P, t.F, t.Fi, t.Vasos }.Any(x => !double.IsFinite(x) || x <= 0) || t.Vasos > 40)
            Invalid("Las metas deben ser positivas y finitas; revisa los overrides.");
    }
    private static void SetTargets(NutritionPrescription plan, NutritionTargets t)
    {
        plan.TargetsJson = Write(t); plan.CalorieTarget = t.K; plan.CarbsTarget = t.C; plan.ProteinTarget = t.P;
        plan.FatTarget = t.F; plan.FiberTarget = t.Fi; plan.WaterMl = (int)(t.Vasos * 250);
    }
    private static TimeOnly Time(string type) => type switch { "des" => new(7, 0), "alm" => new(12, 0), "mer" => new(15, 30), "cen" => new(19, 0), _ => throw new UnprocessableEntityException("Comida inválida.") };
    private static NutritionPrescriptionMeal ToMeal(NutritionMealEdit input) => new()
    {
        DayNumber = input.DayNumber, MealType = input.MealType, ScheduledTime = Time(input.MealType), MenuName = input.MenuName,
        Notes = input.Notes, WaterMl = input.WaterMl, Items = input.Items.Select((i, n) => new NutritionPrescriptionItem
            { FoodItemId = i.FoodItemId, Grams = i.Grams, SortOrder = n, Notes = i.Notes }).ToList()
    };
    private static NutritionMealEdit EditOf(NutritionPrescriptionMeal m) => new(m.DayNumber, m.MealType, m.MenuName,
        m.Items.OrderBy(i => i.SortOrder).Select(i => new NutritionItemEdit(i.FoodItemId, i.Grams, i.Notes)).ToArray(), m.Notes, m.WaterMl);
    private static NutritionDay[] Days(NutritionPrescription p) => p.Meals.GroupBy(m => m.DayNumber).OrderBy(x => x.Key)
        .Select(g => new NutritionDay(g.Key, [], g.ToDictionary(m => m.MealType,
            m => m.Items.OrderBy(i => i.SortOrder).Select(i => new NutritionItem(i.FoodItemId, i.Grams)).ToArray()))).ToArray();
    private static IReadOnlyList<NutritionAlert> Flags(NutritionPrescription p, NutritionEngine e)
        => e.AllFlags(Read<NutritionPatient>(p.ClinicalProfileJson), Read<NutritionTargets>(p.TargetsJson), Days(p));
    private static void ValidateMeals(NutritionPrescription p, NutritionEngine engine)
    {
        if (p.Meals.Count != 120 || p.Meals.Any(m => m.DayNumber is < 1 or > 30 || !NutritionEngine.MealOrder.Contains(m.MealType))
            || p.Meals.Select(m => (m.DayNumber, m.MealType)).Distinct().Count() != 120) Invalid("Se requieren 30 días y cuatro comidas por día.");
        foreach (var meal in p.Meals)
        {
            if (meal.Items.Count is 0 or > 20 || meal.MenuName?.Length > 200 || meal.WaterMl < 0) Invalid("Comida inválida.");
            foreach (var i in meal.Items)
            {
                if (!double.IsFinite(i.Grams) || i.Grams <= 0 || i.Grams > 2000) Invalid("Gramos inválidos.");
                try { engine.ItemMac(new(i.FoodItemId, i.Grams)); } catch (KeyNotFoundException) { Invalid("Alimento fuera del catálogo."); }
            }
        }
    }
    private static void ApplyEdits(NutritionPrescription plan, NutritionEdit edit, NutritionEngine engine, bool applyDay)
    {
        if (edit.Targets is { } t)
        {
            ValidateTargets(t); var current = Read<NutritionTargets>(plan.TargetsJson);
            SetTargets(plan, current with { K = t.K, C = t.C, P = t.P, F = t.F, Fi = t.Fi, Vasos = t.Vasos }); plan.MenusOutdated = true;
        }
        if (edit.Meals is { } meals)
        {
            if (meals.Count > 120 || meals.Select(m => (m.DayNumber, m.MealType)).Distinct().Count() != meals.Count) Invalid("Comidas duplicadas o excesivas.");
            foreach (var meal in meals)
            {
                var old = plan.Meals.SingleOrDefault(x => x.DayNumber == meal.DayNumber && x.MealType == meal.MealType);
                if (old is not null) plan.Meals.Remove(old); plan.Meals.Add(ToMeal(meal));
            }
        }
        if (applyDay)
        {
            if (edit.SourceDay is not (>= 1 and <= 30)) Invalid("Indica el día a aplicar.");
            var source = plan.Meals.Where(m => m.DayNumber == edit.SourceDay).Select(EditOf).ToArray();
            if (source.Length != 4) Invalid("El día origen debe tener cuatro comidas.");
            plan.Meals = Enumerable.Range(1, 30).SelectMany(d => source.Select(m => ToMeal(m with { DayNumber = d }))).ToList();
        }
        if (edit.Instructions is not null)
        {
            // La traducción solo es válida para su fuente exacta. Una edición
            // española sin reemplazo inglés no puede conservar texto clínico obsoleto.
            if (!string.Equals(plan.Instructions, edit.Instructions, StringComparison.Ordinal)
                && edit.InstructionsEn is null)
            {
                plan.InstructionsEn = null;
                plan.InstructionsEnSourceHash = null;
            }
            plan.Instructions = edit.Instructions;
        }
        if (edit.InstructionsEn is not null)
        {
            plan.InstructionsEn = edit.InstructionsEn;
            // Una edición solo EN no demuestra qué fuente revisó el profesional.
            // La fuente debe llegar explícitamente con la pareja del mismo guardado.
            plan.InstructionsEnSourceHash = NutritionInstructionTranslation.PairedSourceHash(edit.Instructions, edit.InstructionsEn);
        }
        if (edit.ConfirmManualEdits) { plan.ManualEditsConfirmed = true; if (edit.Meals is not null || applyDay) plan.MenusOutdated = false; }
        ValidateMeals(plan, engine);
    }
    private NutritionPrescription CopyVersion(NutritionPrescription p, Guid actor, int version)
    {
        var copy = new NutritionPrescription { PatientId = p.PatientId, CycleId = p.CycleId, Version = version,
            CreatedBy = actor, CreatedAt = clock.GetUtcNow().UtcDateTime, ClinicalProfileJson = p.ClinicalProfileJson,
            TargetsJson = p.TargetsJson, MissingDataJson = p.MissingDataJson, Provisional = p.Provisional,
            ManualEditsConfirmed = p.ManualEditsConfirmed, Instructions = p.Instructions, InstructionsEn = p.InstructionsEn,
            InstructionsEnSourceHash = p.InstructionsEnSourceHash,
            NarrativeSource = p.NarrativeSource, StartDate = p.StartDate, EndDate = p.EndDate,
            Meals = p.Meals.Select(m => ToMeal(EditOf(m))).ToList() };
        SetTargets(copy, Read<NutritionTargets>(p.TargetsJson)); return copy;
    }
    private async Task EventAsync(NutritionPrescription plan, NutritionCaseCommand command, NutritionEngine engine, CancellationToken ct, string? action = null)
    {
        var flags = Flags(plan, engine);
        await store.AddEventAsync(new() { PrescriptionId = plan.Id, ActorUserId = command.Actor.UserId,
            OccurredAt = clock.GetUtcNow().UtcDateTime, Action = action ?? command.Operation.ToString().ToLowerInvariant(), Revision = plan.Revision,
            Comment = command.Actor.UserId == NutritionSystemActor.Id ? NutritionSystemActor.Name : command.Comment,
            CriticalCount = flags.Count(f => f.S == "crit"), CriticalConfirmed = command.CriticalConfirmed,
            AutoApproved = action is null && command.Operation == NutritionOperation.Approve && plan.CreatedBy == command.Actor.UserId,
            SnapshotJson = Write(new { plan.Id, plan.PatientId, plan.CycleId, plan.Version, plan.Revision, plan.State, plan.Provisional,
                plan.IsPublished, plan.StartDate, plan.EndDate, plan.ManualEditsConfirmed, plan.ClinicalProfileJson, plan.TargetsJson,
                plan.MissingDataJson, plan.Instructions, plan.InstructionsEn, plan.InstructionsEnSourceHash, meals = plan.Meals.Select(EditOf).ToArray(), flags }) }, ct);
    }
    private NutritionView View(NutritionPrescription p, NutritionCaseContext context, NutritionEngine engine, bool includeMeals = true)
        => new(p.Id, p.PatientId, p.CycleId, p.Version, p.Revision, NutritionStateMachine.DisplayState(p, Today(context)), p.Provisional, p.IsPublished, p.MenusOutdated,
            p.ManualEditsConfirmed, p.StartDate, p.EndDate, Read<NutritionTargets>(p.TargetsJson), p.Instructions, p.InstructionsEn,
            p.NarrativeSource, Read<string[]>(p.MissingDataJson), includeMeals ? Flags(p, engine) : [],
            includeMeals ? p.Meals.OrderBy(m => m.DayNumber).ThenBy(m => m.ScheduledTime).Select(EditOf).ToArray() : [],
            p.Provisional && context.TestsComplete, p.CreatedBy, p.ApprovedBy, p.ApprovedAt);
}
