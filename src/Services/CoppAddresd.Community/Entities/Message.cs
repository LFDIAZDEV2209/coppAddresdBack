namespace CoppAddresd.Community.Entities;

public sealed class Message
{
    public Guid Id { get; set; }
    public Guid SenderProfileId { get; set; }
    public Guid RecipientProfileId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
