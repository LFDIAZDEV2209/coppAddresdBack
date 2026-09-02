using CoppAddresd.Application.Features.ProgramProgress;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.BulkEnrollPatients;

/// <summary>
/// Fila de resultado de la inscripción masiva (B13): por paciente, la
/// inscripción creada o el error que la impidió (reporte parcial por fila —
/// un fallo nunca aborta el lote).
/// </summary>
public sealed record BulkEnrollPatientResultDto(
    Guid PatientId,
    Guid? EnrollmentId,
    string? Error);

/// <summary>
/// Resultado agregado de la inscripción masiva (B13): filas en el MISMO orden
/// del request, más los conteos de creadas/fallidas.
/// </summary>
public sealed record BulkEnrollPatientsResultDto(
    IReadOnlyList<BulkEnrollPatientResultDto> Results,
    int Created,
    int Failed);

/// <summary>
/// Inscripción masiva de pacientes al programa (B13, T-29, SPEC §7.5).
/// Reutiliza EXACTAMENTE el camino de <c>EnrollPatientCommand</c> por paciente
/// (idempotencia, plantilla por defecto, lunes de la semana local): el handler
/// despacha un comando por fila y captura el error de cada una. Un fallo
/// individual NO aborta el lote (reporte parcial por fila).
///
/// Nota de alcance (B13): se procesa síncronamente con tope de 100 pacientes
/// por request (el cliente puede trocear lotes mayores). El dispatcher
/// asíncrono (>100 pacientes vía job) queda como trabajo futuro — la
/// infraestructura <c>IJobDispatcher</c>/<c>app.background_jobs</c> aún no
/// existe en el monorepo.
/// </summary>
public sealed record BulkEnrollPatientsCommand(
    IReadOnlyList<Guid> PatientIds,
    Guid? TemplateId,
    string Timezone,
    DateOnly? StartLocalDate,
    string? DefaultTemplateCode = null,
    Guid? ActorId = null) : IRequest<BulkEnrollPatientsResultDto>;

public sealed class BulkEnrollPatientsCommandValidator : AbstractValidator<BulkEnrollPatientsCommand>
{
    public BulkEnrollPatientsCommandValidator()
    {
        RuleFor(x => x.PatientIds)
            .NotEmpty()
            .WithMessage("La lista de patientIds es requerida.")
            .Must(ids => ids.Count <= 100)
            .WithMessage("El lote no puede exceder 100 pacientes por request.");

        RuleForEach(x => x.PatientIds)
            .NotEmpty()
            .WithMessage("Cada patientId debe ser un GUID válido.");

        RuleFor(x => x.Timezone)
            .NotEmpty()
            .WithMessage("La zona horaria es requerida.")
            .Must(ProgramProgressTime.IsValidIanaTimezone)
            .WithMessage("La zona horaria debe ser un identificador IANA válido (ej: America/Bogota).");

        RuleFor(x => x.TemplateId)
            .NotEmpty()
            .WithMessage("El templateId debe ser un GUID válido.")
            .When(x => x.TemplateId.HasValue);

        RuleFor(x => x.StartLocalDate)
            .NotEmpty()
            .When(x => x.StartLocalDate.HasValue)
            .WithMessage("startLocalDate debe ser una fecha válida.");
    }
}
