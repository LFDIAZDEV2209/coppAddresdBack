using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Api.Seeders;

/// <summary>
/// Catálogo base de plantillas de notificación de alertas de Tests de Salud
/// (SPEC A13). Siembra 7 plantillas bilingües (español obligatorio, inglés
/// incluido) por cada canal: <c>community</c> (mensaje completo con
/// recomendaciones y llamado a agendar cita) y <c>sms</c> (versión corta). Cada
/// cuerpo sigue la misma estructura: saludo → resultado → significado → 2-3
/// recomendaciones → invitación a agendar la cita en la app (sección Citas) →
/// firma del equipo.
///
/// Idempotencia: se inserta solo si el <c>Code</c> no existe; nunca sobrescribe
/// plantillas editadas por el equipo clínico. Se ejecuta en todos los entornos
/// (ver Program.cs) para que producción tenga el catálogo disponible; un fallo
/// no bloquea el arranque de la API.
/// </summary>
public sealed class HealthTestNotificationTemplateSeeder(
    IServiceScopeFactory scopeFactory,
    ILogger<HealthTestNotificationTemplateSeeder> logger) : IHostedService
{
    private sealed record TemplateSeedDef(
        string Code,
        string NameEs,
        string NameEn,
        NotificationChannel Channel,
        string BodyEs,
        string BodyEn,
        HealthTestSeverity? Severity = null,
        string? IndicatorCode = null
    );

    private static readonly IReadOnlyList<TemplateSeedDef> Seeds =
    [
        // ── Riesgo crítico ──────────────────────────────────────────────────
        new(
            "HT_CRITICO_COMUNIDAD",
            "Riesgo crítico: seguimiento prioritario",
            "Critical risk: priority follow-up",
            NotificationChannel.community,
            """
            ⚠️ Hola [paciente], tu resultado de [indicador] fue [valor] y se clasificó con severidad [severidad].

            Esto significa que tu caso necesita revisión prioritaria por tu equipo de salud. Te recomendamos:
            1. Agendar una cita de seguimiento en la app (sección Citas).
            2. Mantener tus medicamentos y hábitos actuales hasta la valoración.
            3. Si presentas síntomas intensos (dolor en el pecho, dificultad para respirar o desmayos), acude a urgencias.

            Un profesional revisará tu caso y te contactará pronto.

            — Equipo CoppAddresd
            """,
            """
            ⚠️ Hi [paciente], your [indicador] result was [valor] and it was classified as [severidad] risk.

            This means your case needs a priority review by your health team. We recommend:
            1. Booking a follow-up appointment in the app (Appointments section).
            2. Keeping your current medications and habits until your assessment.
            3. If you have severe symptoms (chest pain, trouble breathing or fainting), go to the emergency room.

            A professional will review your case and contact you soon.

            — CoppAddresd Team
            """,
            HealthTestSeverity.critical
        ),
        new(
            "HT_CRITICO_SMS",
            "Riesgo crítico (SMS)",
            "Critical risk (SMS)",
            NotificationChannel.sms,
            "CoppAddresd: [paciente], tu resultado de [indicador] fue [valor] (severidad [severidad]). Agenda tu cita de seguimiento en la app (Citas) o acude a urgencias si tienes síntomas intensos. — Equipo CoppAddresd",
            "CoppAddresd: [paciente], your [indicador] result was [valor] ([severidad] risk). Book your follow-up appointment in the app (Appointments) or go to the ER if you have severe symptoms. — CoppAddresd Team",
            HealthTestSeverity.critical
        ),
        // ── Riesgo alto ─────────────────────────────────────────────────────
        new(
            "HT_ALTO_COMUNIDAD",
            "Riesgo alto: agenda tu seguimiento",
            "High risk: book your follow-up",
            NotificationChannel.community,
            """
            Hola [paciente], tu resultado de [indicador] fue [valor] (severidad [severidad]).

            Tus valores están por encima del rango esperado. Para cuidar tu salud te recomendamos:
            1. Agendar una cita de seguimiento en la app (sección Citas).
            2. Revisar tu alimentación y tu actividad física con tu equipo.
            3. Registrar tus mediciones y síntomas en la app para que podamos acompañarte.

            — Equipo CoppAddresd
            """,
            """
            Hi [paciente], your [indicador] result was [valor] ([severidad] risk).

            Your values are above the expected range. To take care of your health we recommend:
            1. Booking a follow-up appointment in the app (Appointments section).
            2. Reviewing your diet and physical activity with your team.
            3. Logging your measurements and symptoms in the app so we can support you.

            — CoppAddresd Team
            """,
            HealthTestSeverity.high
        ),
        new(
            "HT_ALTO_SMS",
            "Riesgo alto (SMS)",
            "High risk (SMS)",
            NotificationChannel.sms,
            "CoppAddresd: [paciente], tu [indicador] fue [valor] (severidad [severidad]). Agenda tu cita de seguimiento en la app (Citas) y revisa tu alimentación y actividad. — Equipo CoppAddresd",
            "CoppAddresd: [paciente], your [indicador] was [valor] ([severidad] risk). Book your follow-up appointment in the app (Appointments) and review your diet and activity. — CoppAddresd Team",
            HealthTestSeverity.high
        ),
        // ── Riesgo medio ────────────────────────────────────────────────────
        new(
            "HT_MEDIO_COMUNIDAD",
            "Riesgo medio: control preventivo",
            "Moderate risk: preventive check",
            NotificationChannel.community,
            """
            Hola [paciente], tu resultado de [indicador] fue [valor] (severidad [severidad]).

            Estás en un rango intermedio: no es urgente, pero conviene actuar para evitar complicaciones. Te sugerimos:
            1. Agendar una cita de control en la app (sección Citas).
            2. Cuidar tu alimentación, tu hidratación y tu actividad física.
            3. Repetir tu medición cuando tu equipo te lo indique.

            — Equipo CoppAddresd
            """,
            """
            Hi [paciente], your [indicador] result was [valor] ([severidad] risk).

            You are in an intermediate range: it is not urgent, but acting now prevents complications. We suggest:
            1. Booking a check-up appointment in the app (Appointments section).
            2. Taking care of your diet, hydration and physical activity.
            3. Repeating your measurement when your team tells you to.

            — CoppAddresd Team
            """,
            HealthTestSeverity.moderate
        ),
        new(
            "HT_MEDIO_SMS",
            "Riesgo medio (SMS)",
            "Moderate risk (SMS)",
            NotificationChannel.sms,
            "CoppAddresd: [paciente], tu [indicador] fue [valor] (severidad [severidad]). Agenda una cita de control en la app (Citas) y cuida tu alimentación y actividad. — Equipo CoppAddresd",
            "CoppAddresd: [paciente], your [indicador] was [valor] ([severidad] risk). Book a check-up appointment in the app (Appointments) and take care of your diet and activity. — CoppAddresd Team",
            HealthTestSeverity.moderate
        ),
        // ── ORP (riesgo cardiometabólico) ───────────────────────────────────
        new(
            "HT_ORP_COMUNIDAD",
            "ORP elevado: riesgo cardiometabólico",
            "Elevated ORP: cardiometabolic risk",
            NotificationChannel.community,
            """
            Hola [paciente], tu Índice de Riesgo Cardiometabólico (ORP) fue [valor].

            Este índice estima el riesgo de enfermedades cardiometabólicas y tu resultado está elevado. Te recomendamos:
            1. Agendar una cita de seguimiento en la app (sección Citas).
            2. Reducir el consumo de azúcares y ultraprocesados.
            3. Caminar al menos 30 minutos al día y controlar tu presión y glucemia.

            Tu equipo revisará tu caso en la próxima consulta.

            — Equipo CoppAddresd
            """,
            """
            Hi [paciente], your Cardiometabolic Risk Index (ORP) was [valor].

            This index estimates the risk of cardiometabolic disease and your result is elevated. We recommend:
            1. Booking a follow-up appointment in the app (Appointments section).
            2. Reducing sugars and ultra-processed food.
            3. Walking at least 30 minutes a day and monitoring your blood pressure and glucose.

            Your team will review your case at your next visit.

            — CoppAddresd Team
            """,
            IndicatorCode: "ORP"
        ),
        new(
            "HT_ORP_SMS",
            "ORP elevado (SMS)",
            "Elevated ORP (SMS)",
            NotificationChannel.sms,
            "CoppAddresd: [paciente], tu índice ORP fue [valor] (riesgo elevado). Agenda tu cita de seguimiento en la app (Citas) y cuida tu alimentación y actividad. — Equipo CoppAddresd",
            "CoppAddresd: [paciente], your ORP index was [valor] (elevated risk). Book your follow-up appointment in the app (Appointments) and take care of your diet and activity. — CoppAddresd Team",
            IndicatorCode: "ORP"
        ),
        // ── Adherencia al programa ──────────────────────────────────────────
        new(
            "HT_ADHERENCIA_COMUNIDAD",
            "Adherencia baja: retoma tu plan",
            "Low adherence: get back on track",
            NotificationChannel.community,
            """
            Hola [paciente], tu índice de adherencia al programa fue [valor] (severidad [severidad]).

            Notamos que has retomado parcialmente tus actividades y recomendaciones. Para no perder el avance:
            1. Retoma tu plan en la app (nutrición, ejercicio y registro diario).
            2. Agenda una cita con tu equipo en la sección Citas.
            3. Escríbenos por la comunidad si necesitas apoyo.

            — Equipo CoppAddresd
            """,
            """
            Hi [paciente], your program adherence index was [valor] ([severidad] risk).

            We noticed you have partially resumed your activities and recommendations. To keep your progress:
            1. Get back to your plan in the app (nutrition, exercise and daily check-in).
            2. Book an appointment with your team in the Appointments section.
            3. Message us in the community if you need support.

            — CoppAddresd Team
            """,
            IndicatorCode: "ADHERENCIA"
        ),
        new(
            "HT_ADHERENCIA_SMS",
            "Adherencia baja (SMS)",
            "Low adherence (SMS)",
            NotificationChannel.sms,
            "CoppAddresd: [paciente], tu adherencia al programa fue [valor]. Retoma tu plan en la app y agenda una cita en Citas. — Equipo CoppAddresd",
            "CoppAddresd: [paciente], your program adherence was [valor]. Get back to your plan in the app and book an appointment in Appointments. — CoppAddresd Team",
            IndicatorCode: "ADHERENCIA"
        ),
        // ── Apnea del sueño ─────────────────────────────────────────────────
        new(
            "HT_APNEA_COMUNIDAD",
            "Sospecha de apnea: valora tu sueño",
            "Suspected apnea: assess your sleep",
            NotificationChannel.community,
            """
            Hola [paciente], tu tamizaje de sueño reportó [valor] señales compatibles con riesgo de apnea.

            Dormir bien es clave para tu salud. Te recomendamos:
            1. Agendar una cita para valorar un estudio de sueño en la app (sección Citas).
            2. Evitar alcohol y cenas pesadas antes de dormir.
            3. Mantener horarios regulares y registrar cómo duermes en la app.

            — Equipo CoppAddresd
            """,
            """
            Hi [paciente], your sleep screening reported [valor] signals compatible with apnea risk.

            Sleeping well is key to your health. We recommend:
            1. Booking an appointment to assess a sleep study in the app (Appointments section).
            2. Avoiding alcohol and heavy meals before bed.
            3. Keeping regular hours and logging how you sleep in the app.

            — CoppAddresd Team
            """,
            IndicatorCode: "APNEA"
        ),
        new(
            "HT_APNEA_SMS",
            "Sospecha de apnea (SMS)",
            "Suspected apnea (SMS)",
            NotificationChannel.sms,
            "CoppAddresd: [paciente], tu tamizaje de sueño reportó [valor] señales de riesgo de apnea. Agenda una cita en la app (Citas) para valorar un estudio de sueño. — Equipo CoppAddresd",
            "CoppAddresd: [paciente], your sleep screening reported [valor] apnea risk signals. Book an appointment in the app (Appointments) to assess a sleep study. — CoppAddresd Team",
            IndicatorCode: "APNEA"
        ),
        // ── Recordatorio general ────────────────────────────────────────────
        new(
            "HT_GENERAL_COMUNIDAD",
            "Recordatorio de salud general",
            "General health reminder",
            NotificationChannel.community,
            """
            Hola [paciente], este es un recordatorio de tu salud: tu resultado de [indicador] fue [valor].

            Mantener tus controles al día es la mejor forma de prevenir. Te recomendamos:
            1. Agendar tu próxima cita de control en la app (sección Citas).
            2. Continuar con tu plan de nutrición y actividad física.
            3. Registrar tus mediciones y síntomas en la app.

            — Equipo CoppAddresd
            """,
            """
            Hi [paciente], this is a reminder about your health: your [indicador] result was [valor].

            Keeping your check-ups up to date is the best way to prevent disease. We recommend:
            1. Booking your next check-up in the app (Appointments section).
            2. Continuing with your nutrition and physical activity plan.
            3. Logging your measurements and symptoms in the app.

            — CoppAddresd Team
            """
        ),
        new(
            "HT_GENERAL_SMS",
            "Recordatorio de salud (SMS)",
            "Health reminder (SMS)",
            NotificationChannel.sms,
            "CoppAddresd: [paciente], tu [indicador] fue [valor]. Agenda tu próxima cita de control en la app (Citas) y continúa con tu plan. — Equipo CoppAddresd",
            "CoppAddresd: [paciente], your [indicador] was [valor]. Book your next check-up in the app (Appointments) and continue with your plan. — CoppAddresd Team"
        ),
    ];

    public Task StartAsync(CancellationToken cancellationToken) => SeedAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var existingCodes = await db
                .HealthTestNotificationTemplates.Select(t => t.Code)
                .ToListAsync(ct);
            var existing = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var created = 0;
            foreach (var seed in Seeds)
            {
                if (existing.Contains(seed.Code))
                {
                    continue;
                }

                var template = new HealthTestNotificationTemplate
                {
                    Id = Guid.NewGuid(),
                    Code = seed.Code,
                    NameEs = seed.NameEs,
                    NameEn = seed.NameEn,
                    Channel = seed.Channel,
                    Severity = seed.Severity,
                    IndicatorCode = seed.IndicatorCode,
                    BodyTemplateEs = seed.BodyEs.Trim(),
                    BodyTemplateEn = seed.BodyEn.Trim(),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                };

                db.HealthTestNotificationTemplates.Add(template);
                db.HealthTestNotificationTemplateVersions.Add(
                    new HealthTestNotificationTemplateVersion
                    {
                        Id = Guid.NewGuid(),
                        TemplateId = template.Id,
                        Version = 1,
                        NameEs = template.NameEs,
                        NameEn = template.NameEn,
                        Channel = template.Channel,
                        Severity = template.Severity,
                        IndicatorCode = template.IndicatorCode,
                        BodyTemplateEs = template.BodyTemplateEs,
                        BodyTemplateEn = template.BodyTemplateEn,
                        Note = "Catálogo base bilingüe (seeder)",
                        CreatedAt = DateTime.UtcNow,
                    }
                );
                created++;
            }

            if (created > 0)
            {
                await db.SaveChangesAsync(ct);
            }

            logger.LogInformation(
                "Catálogo de plantillas de notificación: {Created} creadas, {Existing} ya existían.",
                created,
                existing.Count
            );
        }
        catch (Exception ex)
        {
            // Nunca bloquea el arranque de la API.
            logger.LogWarning(
                ex,
                "No se pudo sembrar el catálogo de plantillas de notificación."
            );
        }
    }
}
