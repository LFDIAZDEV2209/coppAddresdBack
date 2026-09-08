using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.ProgramProgress;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.EnrollPatient;

/// <summary>
/// Orquesta la inscripción de un paciente (SPEC §7.5):
/// 1. Valida la zona IANA (defensa en profundidad; el pipeline ya lo hizo).
/// 2. Resuelve la plantilla: la explícita o, si no viene, la de código
///    <paramref name="EnrollPatientCommand.DefaultTemplateCode"/> (la capa API
///    lee <c>Program:DefaultTemplate:Code</c>; fallback <c>default-83w</c>).
/// 3. <c>StartLocalDate</c> por defecto: el lunes de la semana local actual.
/// 4. Delega en <c>EnrollAsync</c> (que purga los puntajes de la corrida
///    anterior, SPEC §13.7.3) y devuelve la inscripción con su estado de
///    gamificación (racha 0, XP 0) leyéndola de vuelta.
/// 5. Tras el commit, invalida el caché del historial de puntajes del
///    paciente (<c>scores-history:{{patientId}}:v1</c>): la serie cacheada de
///    la corrida anterior (TTL 5 min) no debe servirse a la nueva corrida
///    (patrón league-preferences — la invalidación post-commit del handler).
/// El actor (created_by) llega en <c>ActorId</c> desde la capa API
/// (<c>ICurrentContext</c>), precedente del repo (CreateNutritionPlanCommand).
/// </summary>
public sealed class EnrollPatientCommandHandler(
    IProgramRepository repository,
    ICacheService cache,
    ILogger<EnrollPatientCommandHandler> logger) : IRequestHandler<EnrollPatientCommand, ProgramEnrollmentDto>
{
    public async Task<ProgramEnrollmentDto> Handle(EnrollPatientCommand request, CancellationToken ct)
    {
        if (!ProgramProgressTime.IsValidIanaTimezone(request.Timezone))
        {
            throw new UnprocessableEntityException(
                "INVALID_TIMEZONE: la zona horaria debe ser un identificador IANA válido.");
        }

        var templateId = request.TemplateId;
        if (templateId is null)
        {
            var defaultCode = string.IsNullOrWhiteSpace(request.DefaultTemplateCode)
                ? "default-83w"
                : request.DefaultTemplateCode.Trim();
            var defaultTemplate = await repository.GetTemplateByCodeAsync(defaultCode, ct)
                ?? throw new NotFoundException($"TEMPLATE_NOT_FOUND: plantilla por defecto '{defaultCode}' no encontrada.");
            templateId = defaultTemplate.Id;
        }

        var startLocalDate = request.StartLocalDate
            ?? ProgramProgressTime.MondayOfWeek(ProgramProgressTime.PatientLocalToday(request.Timezone));

        var enrollment = await repository.EnrollAsync(
            request.PatientId, templateId.Value, request.Timezone, startLocalDate, request.ActorId, ct);

        // Post-commit (EnrollAsync ya commiteó): la serie cacheada de la
        // corrida anterior no debe servirse a la nueva (historia por
        // programa, SPEC §13.7.3). Best-effort (fail-open de la abstracción).
        await cache.RemoveAsync(CacheKeys.ScoresHistory(request.PatientId), ct);

        var dto = await repository.GetEnrollmentAsync(enrollment.Id, ct)
            ?? throw new InvalidOperationException("No se pudo leer la inscripción creada.");

        logger.LogInformation(
            "Program.Enroll: enrollment={EnrollmentId} paciente={PatientId} " +
            "plantilla={TemplateId} zona={Timezone} inicio={StartLocalDate}",
            enrollment.Id, request.PatientId, templateId.Value, request.Timezone, startLocalDate);

        return dto;
    }
}