using CoppAddresd.Application.Features.Nutrition.Cases;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;

namespace CoppAddresd.UnitTests.Nutrition;

public sealed class NutritionStateMachineTests
{
    public static IEnumerable<object[]> Transitions()
    {
        foreach (var state in new[] { "borrador_ia", "en_aprobacion", "devuelto", "aprobado" })
        foreach (var operation in Enum.GetValues<NutritionOperation>().Where(x => x != NutritionOperation.Generate))
        {
            var allowed = operation switch { NutritionOperation.Publish => state is "borrador_ia" or "en_aprobacion" or "devuelto",
                NutritionOperation.CreateVersion => state == "aprobado",
                NutritionOperation.Approve or NutritionOperation.Return => state == "en_aprobacion",
                _ => state is "borrador_ia" or "devuelto" };
            yield return [state, operation, allowed];
        }
    }
    [Theory]
    [MemberData(nameof(Transitions))]
    public void OnlyDeclaredTransitionsAreAllowed(string state, NutritionOperation operation, bool allowed)
    {
        var error = Record.Exception(() => NutritionStateMachine.Ensure(operation, state));
        if (allowed) Assert.Null(error); else Assert.IsType<BusinessRuleViolationException>(error);
    }
    [Theory]
    [InlineData("2026-11-06T02:00:00Z", "America/Bogota", "2026-11-05")]
    [InlineData("2026-11-06T02:00:00Z", "Asia/Tokyo", "2026-11-06")]
    public void CalendarUsesPatientTimezone(string instant, string timezone, string expected)
        => Assert.Equal(DateOnly.Parse(expected), NutritionStateMachine.Today(DateTimeOffset.Parse(instant), timezone));
    [Theory]
    [InlineData(6, "aprobado")] [InlineData(5, "por_renovar")] [InlineData(0, "por_renovar")] [InlineData(-1, "por_renovar")]
    public void RenewalIsVirtualIncludingExpiredCycles(int remaining, string expected)
    {
        var today = new DateOnly(2026, 11, 5);
        var p = new NutritionPrescription { State = "aprobado", IsPublished = true, EndDate = today.AddDays(remaining) };
        Assert.Equal(expected, NutritionStateMachine.DisplayState(p, today)); Assert.Equal("aprobado", p.State);
    }
    [Fact]
    public void CycleDates_AreThirtyInclusiveDays_AndNewVersionCannotRedate()
    {
        var today = new DateOnly(2026, 11, 5); var dates = NutritionStateMachine.Dates(today, null, null);
        Assert.Equal(29, dates.End.DayNumber - dates.Start.DayNumber);
        var previous = new NutritionPrescription { StartDate = dates.Start, EndDate = dates.End };
        Assert.Equal(dates, NutritionStateMachine.Dates(today.AddDays(10), null, previous));
        Assert.Throws<BusinessRuleViolationException>(() => NutritionStateMachine.Dates(today, today.AddDays(1), previous));
        Assert.Throws<BusinessRuleViolationException>(() => NutritionStateMachine.Dates(today.AddDays(30), null, previous));
        Assert.Throws<UnprocessableEntityException>(() => NutritionStateMachine.Dates(today, today.AddDays(-1), null));
    }
}
