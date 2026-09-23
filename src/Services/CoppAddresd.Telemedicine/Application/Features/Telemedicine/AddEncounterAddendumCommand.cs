using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Agrega una adenda a un encuentro clínico completado (F4, append-only). La
/// autorización es la del encuentro (profesional asignado o supervisor;
/// paciente → 403). Exige un encuentro existente y <c>Completed</c>: en
/// borrador el registro se edita normalmente (409). El autor sale del JWT; el
/// nombre se guarda como snapshot legible.
/// </summary>
public sealed record AddEncounterAddendumCommand(
    Guid AppointmentId,
    string Body,
    string? AuthorName,
    Guid UserId,
    bool HasManagePermission) : IRequest<EncounterAddendumDto>;

public sealed class AddEncounterAddendumCommandValidator
    : AbstractValidator<AddEncounterAddendumCommand>
{
    /// <summary>Longitud máxima del texto de la adenda (coincide con la columna).</summary>
    public const int MaxBodyLength = 2000;

    public AddEncounterAddendumCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Body)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("La adenda no puede estar vacía.")
            .Must(body => body.Trim().Length > 0)
            .WithMessage("La adenda no puede estar vacía.")
            .Must(body => body.Trim().Length <= MaxBodyLength)
            .WithMessage($"La adenda no puede superar los {MaxBodyLength} caracteres.");
    }
}

public sealed class AddEncounterAddendumCommandHandler(
    IAppointmentRepository appointments,
    IEncounterRepository encounters,
    IEncounterAddendumRepository addenda,
    IAppointmentReferenceDataService referenceData,
    ILogger<AddEncounterAddendumCommandHandler> logger)
    : IRequestHandler<AddEncounterAddendumCommand, EncounterAddendumDto>
{
    /// <summary>Longitud máxima del snapshot de nombre del autor (coincide con la columna).</summary>
    public const int MaxAuthorNameLength = 200;

    public async Task<EncounterAddendumDto> Handle(
        AddEncounterAddendumCommand request,
        CancellationToken ct)
    {
        var appointment = await appointments.GetByIdAsync(request.AppointmentId, ct)
            ?? throw new NotFoundException("Cita", request.AppointmentId);

        await SessionSupport.RequireSessionOwnerAsync(
            referenceData, appointment, request.UserId, request.HasManagePermission, ct);

        var encounter = await encounters.GetByAppointmentIdAsync(appointment.Id, ct);
        if (encounter is null || encounter.Status != EncounterStatus.Completed)
        {
            throw new BusinessRuleViolationException(
                "La adenda solo puede agregarse a un encuentro completado.");
        }

        var addendum = new EncounterAddendum
        {
            EncounterId = encounter.Id,
            AuthorUserId = request.UserId,
            AuthorName = SnapshotAuthorName(request.AuthorName),
            Body = request.Body.Trim(),
            CreatedAt = DateTime.UtcNow,
        };

        await addenda.AddAsync(addendum, ct);

        // Log sin PHI: solo ids, autor y longitud del texto.
        logger.LogInformation(
            "Adenda {AddendumId} agregada al encuentro {EncounterId} de la cita {AppointmentId} por {AuthorUserId} ({BodyLength} caracteres).",
            addendum.Id, encounter.Id, appointment.Id, request.UserId, addendum.Body.Length);

        return EncounterAddendumDto.FromEntity(addendum);
    }

    /// <summary>
    /// Snapshot legible del autor desde el claim del JWT: trim, vacío → null y
    /// truncado al máximo de la columna (el claim lo controla el emisor, un
    /// nombre largo no debe romper el INSERT).
    /// </summary>
    private static string? SnapshotAuthorName(string? authorName)
    {
        if (string.IsNullOrWhiteSpace(authorName))
        {
            return null;
        }

        var trimmed = authorName.Trim();
        return trimmed.Length <= MaxAuthorNameLength
            ? trimmed
            : trimmed[..MaxAuthorNameLength];
    }
}
