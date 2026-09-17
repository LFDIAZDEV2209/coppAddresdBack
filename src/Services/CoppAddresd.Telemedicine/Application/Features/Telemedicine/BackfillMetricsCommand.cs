using CoppAddresd.Telemedicine.Application.Interfaces;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Recalcula las métricas pre-agregadas de analytics
/// (<c>tele.appointment_daily_metrics</c> y <c>tele.professional_daily_stats</c>)
/// a partir de <c>tele.appointments</c> para un rango de días de agenda.
///
/// Casos de uso: carga inicial tras crear las tablas, reparación de deriva
/// (el processor incremental no descuenta reprogramaciones entre días ni
/// recupera eventos perdidos) y recálculo tras cambios de reglas. Es
/// idempotente y re-ejecutable: cada fila se sobrescribe con el recálculo
/// autoritativo, nunca se suma.
///
/// Con <c>DryRun</c> no escribe nada y reporta lo que escribiría.
/// </summary>
/// <param name="From">Día de agenda inicial (UTC). Nulo = historia completa.</param>
/// <param name="To">
/// Día de agenda final (UTC). Nulo = hoy. Admite futuro: las citas programadas
/// a futuro también llevan filas pre-agregadas (igual que las que escribe el
/// processor incremental al crearlas) y los lectores prefieren el pre-agregado
/// cuando existe; la carga inicial debe cubrirlas o los rangos que toquen
/// futuro quedarían subcontados.
/// </param>
/// <param name="ClinicId">Acota a una clínica. Nulo = todas.</param>
/// <param name="DryRun">Simula sin escribir.</param>
public sealed record BackfillMetricsCommand(
    DateTimeOffset? From,
    DateTimeOffset? To,
    Guid? ClinicId,
    bool DryRun = false
) : IRequest<BackfillMetricsResult>;

/// <summary>Resumen de un backfill ejecutado (o simulado con <c>DryRun</c>).</summary>
public sealed record BackfillMetricsResult(
    DateOnly From,
    DateOnly To,
    bool DryRun,
    long AppointmentsConsidered,
    long MetricsRows,
    long StatsRows,
    TimeSpan Duration,
    string Note
);

public sealed class BackfillMetricsCommandValidator : AbstractValidator<BackfillMetricsCommand>
{
    /// <summary>Rango máximo aceptado: 10 años (cota contra errores de tipeo).</summary>
    private const int MaxRangeDays = 3650;

    public BackfillMetricsCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => x.From is null || x.To is null || x.From <= x.To)
            .WithMessage("El inicio del rango no puede ser posterior al fin.");

        // Sin cota de futuro: el To admite días de agenda futuros para que la
        // carga inicial cubra las citas ya programadas. La cota de 10 años
        // sigue protegiendo contra errores de tipeo.
        RuleFor(x => x)
            .Must(x =>
                x.From is null
                || x.To is null
                || (x.To.Value - x.From.Value).TotalDays <= MaxRangeDays
            )
            .WithMessage($"El rango no puede superar {MaxRangeDays} días.");
    }
}

public sealed class BackfillMetricsCommandHandler(IMetricsBackfillService backfill)
    : IRequestHandler<BackfillMetricsCommand, BackfillMetricsResult>
{
    public Task<BackfillMetricsResult> Handle(BackfillMetricsCommand request, CancellationToken ct)
    {
        var to = (request.To ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var from = (request.From ?? DateTimeOffset.MinValue).ToUniversalTime();
        if (from > to)
        {
            (from, to) = (to, from);
        }

        return backfill.BackfillAsync(
            DateOnly.FromDateTime(from.UtcDateTime),
            DateOnly.FromDateTime(to.UtcDateTime),
            request.ClinicId,
            request.DryRun,
            ct
        );
    }
}
