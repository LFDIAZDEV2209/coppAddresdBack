using CoppAddresd.Application.Features.Patients.Events;
using CoppAddresd.Application.Interfaces;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Application.Features.Patients;

/// <summary>
/// Cambio de estado operativo del paciente desde el listado (toggle
/// Activo↔Inactivo). Endpoint dedicado: el comando de actualización de
/// agregado reemplaza las colecciones hijas (diagnósticos, medicamentos,
/// alergias, vitales) y no sirve como actualización parcial. Solo se permite
/// <c>Activo</c>/<c>Inactivo</c> (<see cref="PatientOptions.Statuses"/>);
/// cualquier otro valor se rechaza con 400 en la validación.
/// </summary>
public record UpdatePatientStatusCommand(Guid Id, string Status, Guid? UpdatedBy)
    : IRequest<PatientStatusResultDto?>;

/// <summary>Resultado mínimo del cambio de estado (id + estado vigente).</summary>
public record PatientStatusResultDto(Guid Id, string Status);

public sealed class UpdatePatientStatusCommandValidator
    : AbstractValidator<UpdatePatientStatusCommand>
{
    public UpdatePatientStatusCommandValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(status => PatientOptions.Statuses.Contains(status))
            .WithMessage("El estado debe ser Activo o Inactivo.");
    }
}

public sealed class UpdatePatientStatusCommandHandler(
    IPatientRepository repository,
    IPatientMetricsQueue? metricsQueue = null
) : IRequestHandler<UpdatePatientStatusCommand, PatientStatusResultDto?>
{
    public async Task<PatientStatusResultDto?> Handle(
        UpdatePatientStatusCommand request,
        CancellationToken ct
    )
    {
        var snapshot = await repository.GetStatusSnapshotAsync(request.Id, ct);
        if (snapshot is null)
            return null;

        var status = request.Status.Trim();

        // Idempotente: repetir el mismo estado no escribe ni duplica la métrica.
        if (string.Equals(snapshot.Status, status, StringComparison.Ordinal))
            return new PatientStatusResultDto(request.Id, snapshot.Status);

        await repository.UpdateStatusAsync(request.Id, status, request.UpdatedBy, ct);

        // Pre-agregación CQRS (misma señal que la edición completa): el
        // procesador ajusta los contadores status_count por clínica.
        if (metricsQueue is not null)
        {
            await metricsQueue.EnqueueAsync(
                new PatientStatusChangedMetricEvent(
                    request.Id,
                    snapshot.ClinicId,
                    snapshot.Status,
                    status,
                    DateTime.UtcNow
                )
            );
        }

        return new PatientStatusResultDto(request.Id, status);
    }
}
