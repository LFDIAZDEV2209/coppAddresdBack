using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Application.Features.Professionals;

/// <summary>Payload de una franja horaria en el contrato JSON del PUT.</summary>
public record ScheduleSlotInput(int Weekday, string StartTime, string EndTime);

/// <summary>
/// Reemplaza los horarios semanales de un profesional (total-replace:
/// borra todos los existentes e inserta los nuevos).
/// </summary>
public record PutProfessionalSchedulesCommand(
    Guid ProfessionalId,
    IReadOnlyList<ScheduleSlotInput> Schedules
) : IRequest<Unit>;

public sealed class PutProfessionalSchedulesCommandHandler(
    IEmployeeRepository repository
) : IRequestHandler<PutProfessionalSchedulesCommand, Unit>
{
    public async Task<Unit> Handle(
        PutProfessionalSchedulesCommand request,
        CancellationToken ct
    )
    {
        // Verificar que el profesional existe.
        var employee = await repository.GetByProfessionalIdAsync(request.ProfessionalId, ct);
        if (employee is null || employee.Professional is null)
            throw new UnprocessableEntityException("El profesional no existe.");

        // Parsear y convertir las entradas a entidades.
        var schedules = request
            .Schedules.Select(s => new ProfessionalSchedule
            {
                ProfessionalId = request.ProfessionalId,
                Weekday = s.Weekday,
                StartTime = TimeOnly.Parse(s.StartTime),
                EndTime = TimeOnly.Parse(s.EndTime),
            })
            .ToList();

        await repository.ReplaceSchedulesAsync(
            request.ProfessionalId,
            schedules,
            ct
        );

        return Unit.Value;
    }
}

/// <summary>Validación FluentValidation del comando PUT de horarios.</summary>
public sealed class PutProfessionalSchedulesCommandValidator
    : AbstractValidator<PutProfessionalSchedulesCommand>
{
    public PutProfessionalSchedulesCommandValidator()
    {
        RuleFor(x => x.ProfessionalId).NotEmpty();

        RuleFor(x => x.Schedules).NotNull();

        RuleForEach(x => x.Schedules).ChildRules(slot =>
        {
            slot.RuleFor(s => s.Weekday)
                .InclusiveBetween(1, 7)
                .WithMessage("El día de la semana debe estar entre 1 (lunes) y 7 (domingo).");

            slot.RuleFor(s => s.StartTime)
                .NotEmpty().WithMessage("La hora de inicio es requerida.")
                .Must(BeValidTimeFormat)
                .WithMessage("La hora de inicio debe tener formato HH:mm.");

            slot.RuleFor(s => s.EndTime)
                .NotEmpty().WithMessage("La hora de fin es requerida.")
                .Must(BeValidTimeFormat)
                .WithMessage("La hora de fin debe tener formato HH:mm.");

            slot.RuleFor(s => s)
                .Must(s =>
                {
                    if (!TimeOnly.TryParse(s.StartTime, out var start) ||
                        !TimeOnly.TryParse(s.EndTime, out var end))
                        return true; // Ya validado arriba.
                    return start < end;
                })
                .WithMessage("La hora de inicio debe ser anterior a la hora de fin.");
        });

        // No duplicados de día de la semana.
        RuleFor(x => x.Schedules)
            .Must(schedules =>
            {
                if (schedules is null) return true;
                return schedules.GroupBy(s => s.Weekday).All(g => g.Count() == 1);
            })
            .WithMessage("No se permiten días de la semana duplicados.");
    }

    private static bool BeValidTimeFormat(string value) =>
        TimeOnly.TryParse(value, out _);
}
