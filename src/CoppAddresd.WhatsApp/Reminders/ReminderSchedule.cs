namespace CoppAddresd.WhatsApp.Reminders;

public static class ReminderSchedule
{
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

    public static DateTimeOffset InWindow(DateTimeOffset candidate, ReminderSettings settings)
    {
        settings.Validate();
        var local = TimeZoneInfo.ConvertTime(candidate, Zone).DateTime;
        if (local.Hour >= settings.EndHour) local = local.Date.AddDays(1).AddHours(settings.StartHour);
        if (local.Hour < settings.StartHour) local = local.Date.AddHours(settings.StartHour);
        while (local.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            local = local.Date.AddDays(1).AddHours(settings.StartHour);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zone));
    }

    public static DateTimeOffset Daily(DateTimeOffset now, DateTimeOffset? lastAccepted, ReminderSettings settings)
    {
        var today = TimeZoneInfo.ConvertTime(now, Zone).Date.AddHours(settings.DailyHour);
        var scheduled = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(today, DateTimeKind.Unspecified), Zone));
        if (scheduled < now) scheduled = now;
        if (lastAccepted is { } previous && scheduled < previous.AddHours(24)) scheduled = previous.AddHours(24);
        return InWindow(scheduled, settings);
    }

    public static TimeSpan Backoff(int attempt, int repeatEveryMinutes = 120) =>
        TimeSpan.FromMinutes(repeatEveryMinutes * Math.Pow(2, Math.Clamp(attempt - 1, 0, 4)));
}
