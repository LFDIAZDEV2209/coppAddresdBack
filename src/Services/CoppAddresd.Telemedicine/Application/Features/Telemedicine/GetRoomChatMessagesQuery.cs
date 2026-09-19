using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Mensajes del chat de la consulta (F3), en orden <c>(created_at, id)</c>
/// ascendente y con cursor incremental <c>after</c>/<c>afterId</c> para el
/// polling del cliente. Autorización idéntica a la sala (participante o
/// supervisor).
/// </summary>
public sealed record GetRoomChatMessagesQuery(
    Guid AppointmentId,
    Guid UserId,
    bool HasManagePermission,
    DateTimeOffset? After = null,
    Guid? AfterId = null,
    int Limit = GetRoomChatMessagesQuery.DefaultLimit) : IRequest<IReadOnlyList<ChatMessageDto>>
{
    /// <summary>Tamaño de página por defecto del polling incremental.</summary>
    public const int DefaultLimit = 50;

    /// <summary>Tamaño de página máximo aceptado.</summary>
    public const int MaxLimit = 100;
}

public sealed class GetRoomChatMessagesQueryValidator : AbstractValidator<GetRoomChatMessagesQuery>
{
    public GetRoomChatMessagesQueryValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Limit)
            .InclusiveBetween(1, GetRoomChatMessagesQuery.MaxLimit)
            .WithMessage($"El límite debe estar entre 1 y {GetRoomChatMessagesQuery.MaxLimit}.");
    }
}

public sealed class GetRoomChatMessagesQueryHandler(
    IAppointmentRepository appointments,
    IChatMessageRepository messages,
    IAppointmentReferenceDataService referenceData)
    : IRequestHandler<GetRoomChatMessagesQuery, IReadOnlyList<ChatMessageDto>>
{
    public async Task<IReadOnlyList<ChatMessageDto>> Handle(
        GetRoomChatMessagesQuery request,
        CancellationToken ct)
    {
        var appointment = await appointments.GetByIdAsync(request.AppointmentId, ct)
            ?? throw new NotFoundException("Cita", request.AppointmentId);

        await SessionSupport.RequireParticipantAsync(
            referenceData, appointment, request.UserId, request.HasManagePermission, ct);

        SessionSupport.EnsureChatAllowed(appointment.Status);

        var items = await messages.ListAfterAsync(
            appointment.Id, request.After, request.AfterId, request.Limit, ct);

        return items.Select(ChatMessageDto.FromEntity).ToList();
    }
}
