namespace CoppAddresd.Domain.Entities.WhatsApp;

public enum BindingState { AwaitingDocument, Sending, AwaitingCode, Verifying, Approved, Ended, Expired }

/// <summary>Una sesión por número; conserva las dos reservas recientes incluso al terminar/reiniciar el flujo.</summary>
public sealed class BindingSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string WaId { get; set; } = "";
    public Guid? PatientId { get; set; }
    public string? ContactId { get; set; }
    public BindingState State { get; set; } = BindingState.Ended;
    public int Attempts { get; set; }
    public int OtpSends { get; set; }
    public DateTimeOffset[] OtpSentAt { get; set; } = [];
    public DateTimeOffset ExpiresAt { get; set; }
    public Guid? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public Guid? LastEventId { get; set; }
    public string? LastReply { get; set; }

    public bool IsActive => State is BindingState.AwaitingDocument or BindingState.AwaitingCode;
    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
    public bool CanSend(DateTimeOffset now) => OtpSentAt.Count(t => t > now.AddHours(-1)) < 2;
    public bool CanVerify(DateTimeOffset now) => State == BindingState.AwaitingCode && !IsExpired(now) && Attempts < 3;

    public void Begin(DateTimeOffset now)
    {
        State = BindingState.AwaitingDocument;
        PatientId = null;
        ContactId = null;
        Attempts = 0;
        ExpiresAt = now.AddMinutes(10);
    }

    public void ReserveSend(DateTimeOffset now)
    {
        if (!CanSend(now)) throw new InvalidOperationException("Límite de envíos OTP del canal.");
        OtpSentAt = OtpSentAt.Where(t => t > now.AddHours(-1)).Append(now).ToArray();
        OtpSends = OtpSentAt.Length;
        State = BindingState.Sending;
    }
}
