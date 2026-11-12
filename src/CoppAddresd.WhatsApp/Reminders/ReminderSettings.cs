namespace CoppAddresd.WhatsApp.Reminders;

public sealed class ReminderSettings
{
    public bool Enabled { get; set; }
    public int StartHour { get; set; } = 9;
    public int EndHour { get; set; } = 18;
    public int DailyHour { get; set; } = 10;
    public int ActiveConversationMinutes { get; set; } = 120;

    public void Validate()
    {
        if (StartHour is < 0 or > 23 || EndHour <= StartHour || EndHour > 24
            || DailyHour < StartHour || DailyHour >= EndHour || ActiveConversationMinutes < 1)
            throw new InvalidOperationException("Franja de recordatorios WhatsApp inválida.");
    }
}
