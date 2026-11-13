using CoppAddresd.Domain.Exceptions;

namespace CoppAddresd.Application.Features.ProgramProgress;

/// <summary>Rango semanal del programa; las 12 semanas actuales cubren 83 días.</summary>
public static class ProgramTemplateWeekRules
{
    public static void ValidateWeek(int totalWeeks, int? weekNumber)
    {
        if (weekNumber is { } week && (week < 1 || week > totalWeeks))
            throw new UnprocessableEntityException($"WEEK_NUMBER_OUT_OF_RANGE: semana fuera de [1..{totalWeeks}].");
    }

    public static bool IsAllowedDay(int totalWeeks, int? weekNumber, short weekday) =>
        weekday is >= 1 and <= 7 && !(totalWeeks == 12 && weekNumber == 12 && weekday == 7);
}
