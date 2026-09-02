using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.ProgramProgress.Commands.EnrollPatient;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.BulkEnrollPatients;

/// <summary>
/// Handler de la inscripción masiva (B13, T-29): despacha
/// <see cref="EnrollPatient.EnrollPatientCommand"/> por cada paciente — el
/// MISMO camino que la inscripción individual (validadores, plantilla por
/// defecto, idempotencia 409 por índice único parcial) — y captura el error de
/// cada fila. Log de resumen sin PHI (solo conteos).
/// </summary>
public sealed class BulkEnrollPatientsCommandHandler(
    IMediator mediator,
    ILogger<BulkEnrollPatientsCommandHandler> logger)
    : IRequestHandler<BulkEnrollPatientsCommand, BulkEnrollPatientsResultDto>
{
    public async Task<BulkEnrollPatientsResultDto> Handle(
        BulkEnrollPatientsCommand request, CancellationToken ct)
    {
        var results = new List<BulkEnrollPatientResultDto>(request.PatientIds.Count);
        var created = 0;
        var failed = 0;

        foreach (var patientId in request.PatientIds)
        {
            try
            {
                ProgramEnrollmentDto enrollment = await mediator.Send(
                    new EnrollPatientCommand(
                        patientId,
                        request.TemplateId,
                        request.Timezone,
                        request.StartLocalDate,
                        request.DefaultTemplateCode,
                        request.ActorId),
                    ct);

                results.Add(new BulkEnrollPatientResultDto(patientId, enrollment.Id, null));
                created++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Reporte parcial por fila (AC B13): un fallo nunca aborta el lote.
                results.Add(new BulkEnrollPatientResultDto(
                    patientId, null, $"{ex.GetType().Name}: {ex.Message}"));
                failed++;
            }
        }

        logger.LogInformation(
            "Program.BulkEnroll: total={Total} created={Created} failed={Failed} actor={ActorId}",
            request.PatientIds.Count, created, failed, request.ActorId);

        return new BulkEnrollPatientsResultDto(results, created, failed);
    }
}
