namespace CoppAddresd.Domain.Entities.WhatsApp;

public enum ReminderKind { checkin, evaluacion, racha, podcast }
public enum ReminderState { Pending, Completed, Cancelled, Exhausted }
public enum DeliveryState { Dispatching, Accepted, Retryable, Permanent, Unknown }

public sealed class Reminder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public ReminderKind Kind { get; set; }
    public Guid? AssignmentId { get; set; }
    public string SourceKey { get; set; } = "";
    public string Detail { get; set; } = "";
    public DateTimeOffset ScheduledAt { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public int RepeatEveryMinutes { get; set; } = 120;
    public int MaxAttempts { get; set; } = 5;
    public int Attempts { get; set; }
    public ReminderState State { get; set; }
    public string? CancellationReason { get; set; }
}

public sealed class ReminderDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReminderId { get; set; }
    public Guid PatientId { get; set; }
    public ReminderKind Kind { get; set; }
    public int Attempt { get; set; }
    public string? Wamid { get; set; }
    public DeliveryState State { get; set; }
    public string? ErrorCode { get; set; }
    public DateTimeOffset AttemptedAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
}
