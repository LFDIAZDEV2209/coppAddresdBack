using FluentValidation;

namespace CoppAddresd.Application.Features.Telemedicine;

/// <summary>
/// Validación del payload de
/// <c>POST /api/v1/internal/telemedicine/notifications</c> (F2). Un payload
/// inválido se traduce a 400 por el middleware global (ValidationException);
/// los fallos de proveedor NO pasan por aquí (siempre 200 con estado por canal).
/// </summary>
public sealed class SendTelemedicineNotificationCommandValidator
    : AbstractValidator<SendTelemedicineNotificationCommand>
{
    private static readonly string[] AllowedChannels = ["push", "sms"];

    /// <summary>Límite del título (practical FCM/notification copy).</summary>
    public const int MaxTitleLength = 200;

    /// <summary>Límite del cuerpo: máximo de Twilio Messages (1,600 caracteres).</summary>
    public const int MaxBodyLength = 1600;

    /// <summary>Límite de la clave de dedupe (columna varchar(200)).</summary>
    public const int MaxDedupeKeyLength = 200;

    public SendTelemedicineNotificationCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("El userId es requerido.");

        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("El título es requerido.")
            .MaximumLength(MaxTitleLength)
            .WithMessage($"El título no puede superar {MaxTitleLength} caracteres.");

        RuleFor(x => x.Body)
            .NotEmpty()
            .WithMessage("El body es requerido.")
            .MaximumLength(MaxBodyLength)
            .WithMessage($"El body no puede superar {MaxBodyLength} caracteres.");

        RuleFor(x => x.DedupeKey)
            .MaximumLength(MaxDedupeKeyLength)
            .WithMessage($"La dedupeKey no puede superar {MaxDedupeKeyLength} caracteres.");

        RuleFor(x => x.Channels)
            .NotNull()
            .WithMessage("Los canales son requeridos (Push, Sms).")
            .Must(channels => channels is { Count: > 0 })
            .WithMessage("Debe indicar al menos un canal (Push, Sms).")
            .Must(channels => channels is null || channels.All(IsAllowedChannel))
            .WithMessage("Canales soportados: Push, Sms.");
    }

    private static bool IsAllowedChannel(string? channel) =>
        !string.IsNullOrWhiteSpace(channel)
        && AllowedChannels.Contains(channel.Trim().ToLowerInvariant());
}
