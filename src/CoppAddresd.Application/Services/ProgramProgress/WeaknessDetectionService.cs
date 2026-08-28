using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums.ProgramProgress;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Services.ProgramProgress;

/// <summary>
/// Implementación del servicio de detección de debilidades (SPEC §21, C) con
/// creación de intervenciones derivadas (SPEC §22, "Paso 7d"): reúne el paquete
/// semanal del paciente vía <c>IProgramRepository</c>, lo evalúa con el
/// <see cref="WeaknessRulesEngine"/> (función pura), persiste SOLO las
/// debilidades nuevas (dedupe AC-43) y, para cada debilidad persistida cuya
/// acción sugerida mapea a una intervención, crea la intervención correspondiente
/// con la XP de evaluación (AC-46).
///
/// Mapeo de acciones a tipos de intervención (SPEC §22, C):
/// - <c>create_intervention</c> → tipo根据 la categoría de la debilidad
///   (nutrición → nutrition_adjustment, ejercicio → exercise_adjustment,
///   psicología → psychological_support, otros → plan_adaptation).
/// - <c>telehealth_referral</c> → telehealth_nutrition / telehealth_medical /
///   telehealth_psychology según la categoría.
/// - <c>recovery_mode</c> → recovery_mission.
/// - <c>referral_doctor</c> → telehealth_medical.
/// - <c>referral_nutritionist</c> → telehealth_nutrition.
/// - <c>referral_psychologist</c> → telehealth_psychology.
/// - <c>ai_recommendation</c> / <c>rto_adjustment</c> → plan_adaptation.
///
/// Se dispara SOLO desde <c>POST /scores/calculate</c>, una vez por recálculo
/// (AC-45).
/// </summary>
public sealed class WeaknessDetectionService(
    IProgramRepository repository,
    ILogger<WeaknessDetectionService> logger) : IWeaknessDetectionService
{
    public async Task<WeaknessDetectionResult> DetectAndPersistAsync(
        Guid patientId, CancellationToken ct = default)
    {
        var weekly = await repository.BuildPatientWeeklyDataAsync(patientId, ct: ct);
        if (weekly is null)
        {
            return WeaknessDetectionResult.Empty;
        }

        var descriptors = WeaknessRulesEngine.Evaluate(weekly);
        if (descriptors.Count == 0)
        {
            return WeaknessDetectionResult.Empty;
        }

        // Persistencia con dedupe AC-43: el repositorio omite los códigos con
        // una fila open/acknowledged/in_intervention existente.
        var newWeaknesses = await repository.PersistDetectedWeaknessesAsync(
            patientId, descriptors, ct);

        logger.LogInformation(
            "Program.WeaknessesDetected: patient={PatientId} rules={Rules} " +
            "newPersisted={Persisted} totalDetected={Total}",
            patientId,
            string.Join(",", descriptors.Select(d => d.Code)),
            newWeaknesses.Count,
            descriptors.Count);

        // Crear intervenciones derivadas (SPEC §22, "Paso 7d" — AC-46):
        // para cada debilidad nueva, si su acción mapea a un tipo de
        // intervención, se crea la intervención (con WEAKNESS_ASSESS +20).
        var interventionsCreated = 0;
        foreach (var weakness in newWeaknesses)
        {
            var descriptor = descriptors.FirstOrDefault(d => d.Code == weakness.Code);
            if (descriptor is null)
            {
                continue;
            }

            var interventionType = MapActionToInterventionType(descriptor.Action, descriptor.Category);
            if (interventionType is null)
            {
                // Acción no mapea a intervención (p. ej. latent rules sin acción).
                continue;
            }

            try
            {
                await repository.EnsureInterventionFromWeaknessAsync(
                    patientId, weakness.Id, interventionType.Value,
                    title: weakness.Title,
                    description: weakness.Description,
                    actorId: null,
                    ct);
                interventionsCreated++;
            }
            catch (Exception ex)
            {
                // La creación de intervención no debe romper el flujo de
                // detección (best-effort, mismo patrón que las notificaciones
                // gamificadas SPEC §20, B).
                logger.LogWarning(ex,
                    "Program.InterventionCreateFailed: patient={PatientId} " +
                    "weakness={WeaknessId} code={Code}",
                    patientId, weakness.Id, weakness.Code);
            }
        }

        if (interventionsCreated > 0)
        {
            logger.LogInformation(
                "Program.InterventionsCreated: patient={PatientId} count={Count}",
                patientId, interventionsCreated);
        }

        return new WeaknessDetectionResult(descriptors.Count, newWeaknesses.Count);
    }

    /// <summary>
    /// Mapea la acción sugerida del descriptor de debilidad a un tipo de
    /// intervención (SPEC §22, C). Devuelve null si la acción no genera
    /// intervención (latentes sin acción, etc.).
    /// </summary>
    private static InterventionType? MapActionToInterventionType(
        string action, WeaknessCategory category)
    {
        return action switch
        {
            "create_intervention" => category switch
            {
                WeaknessCategory.nutritional => InterventionType.nutrition_adjustment,
                WeaknessCategory.exercise => InterventionType.exercise_adjustment,
                WeaknessCategory.psychological => InterventionType.psychological_support,
                _ => InterventionType.plan_adaptation,
            },
            "telehealth_referral" => category switch
            {
                WeaknessCategory.nutritional => InterventionType.telehealth_nutrition,
                WeaknessCategory.psychological => InterventionType.telehealth_psychology,
                _ => InterventionType.telehealth_medical,
            },
            "referral_doctor" => InterventionType.telehealth_medical,
            "referral_nutritionist" => InterventionType.telehealth_nutrition,
            "referral_psychologist" => InterventionType.telehealth_psychology,
            "recovery_mode" => InterventionType.recovery_mission,
            "ai_recommendation" => InterventionType.plan_adaptation,
            "rto_adjustment" => InterventionType.plan_adaptation,
            _ => null,
        };
    }
}
