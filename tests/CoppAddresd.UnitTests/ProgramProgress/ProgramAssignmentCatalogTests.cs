using System.Text.Json;
using System.Text.RegularExpressions;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Infrastructure.Migrations;

namespace CoppAddresd.UnitTests.ProgramProgress;

public sealed class ProgramAssignmentCatalogTests
{
    [Fact]
    public void PendingAssignment_SiempreExponeSemanaCero()
    {
        var enrollment = new ProgramEnrollment { CurrentWeekNumber = 3 };
        enrollment.Status = ProgramEnrollmentStatus.PendingAssignment;
        Assert.Equal(0, enrollment.CurrentWeekNumber);
        enrollment.CurrentWeekNumber = 7;
        Assert.Equal(0, enrollment.CurrentWeekNumber);
        enrollment.Status = ProgramEnrollmentStatus.Active;
        enrollment.CurrentWeekNumber = 1;
        Assert.Equal(1, enrollment.CurrentWeekNumber);
    }

    [Fact]
    public void InscripcionManual_ConservaSemanaUnoYEstadoActivo()
    {
        var enrollment = new ProgramEnrollment();
        Assert.Equal(1, enrollment.CurrentWeekNumber);
        Assert.Equal(ProgramEnrollmentStatus.Active, enrollment.Status);
    }

    [Fact]
    public void Catalogo_CincoCondicionesAsignablesConFuentesYDatosFaltantesSinSenal()
    {
        var criteria = ReadCriteria();
        Assert.Equal(7, criteria.Count);
        Assert.Equal(5, criteria.Values.Count(x => x.GetProperty("assignable").GetBoolean()));
        foreach (var code in new[] { "obesidad", "diabetes-t2", "prediabetes", "hipertension", "apnea-sueno" })
        {
            Assert.True(criteria[code].GetProperty("assignable").GetBoolean());
            Assert.Equal("no-match", criteria[code].GetProperty("missingData").GetString());
        }
        var script = AddProgramAssignmentCatalog.ReadSeedScript();
        foreach (var source in new[] { "OMS/NHLBI/ACC-AHA-TOS 2013 y VA/DoD 2026", "ADA Standards of Care 2026 §2", "ADA 2026 §2", "AHA/ACC 2025", "AASM/ATS/CHEST" })
            Assert.Contains(source, script);
        Assert.Equal(126, criteria["diabetes-t2"].GetProperty("all")[1].GetProperty("any")[0].GetProperty("value").GetInt32());
        Assert.Equal(6.5m, criteria["diabetes-t2"].GetProperty("all")[1].GetProperty("any")[1].GetProperty("value").GetDecimal());
        Assert.True(criteria["diabetes-t2"].GetProperty("all")[0].GetProperty("value").GetBoolean());
        Assert.Equal(27.5m, criteria["obesidad"].GetProperty("any")[1].GetProperty("all")[1].GetProperty("value").GetDecimal());
    }

    [Theory]
    [InlineData("phq-9", new int[] { 5, 10, 15, 20 })]
    [InlineData("gad-7", new int[] { 5, 10, 15 })]
    public void Escalas_UmbralesOficialesSinAsignacionDeContenido(string code, int[] cutoffs)
    {
        var criteria = ReadCriteria()[code];
        Assert.False(criteria.GetProperty("assignable").GetBoolean());
        Assert.Equal(cutoffs, criteria.GetProperty("severity").EnumerateArray()
            .Select(x => x.GetProperty("minInclusive").GetInt32()));
    }

    [Fact]
    public void Reglas_CuatroActivasTargetsPorNombreYTresDificultadesSinUmbralesInventados()
    {
        var rules = AddProgramAssignmentCatalog.ReadSeedScript()
            .Split("INSERT INTO app.program_assignment_rules")[1]
            .Split('\n').Select(x => x.Trim()).Where(x => x.StartsWith("('")).ToArray();
        Assert.Equal(5, rules.Length);
        Assert.Equal(4, rules.Count(x => x.TrimEnd(',', ';').EndsWith("true)")));
        foreach (var rule in rules)
        {
            var documents = Regex.Matches(rule, @"'(\{[^']+\})'::jsonb");
            Assert.Equal(2, documents.Count);
            var target = JsonSerializer.Deserialize<JsonElement>(documents[1].Groups[1].Value);
            Assert.DoesNotContain(target.EnumerateObject(), x => x.Name.EndsWith("Id"));
        }
        var movement = rules.Single(x => x.StartsWith("('movimiento-subscales-v1'"));
        var movementTarget = Regex.Matches(movement, @"'(\{[^']+\})'::jsonb")[1].Groups[1].Value;
        var json = JsonSerializer.Deserialize<JsonElement>(movementTarget);
        Assert.Equal(new[] { 1, 2, 3 }, json.GetProperty("difficultyLevels").EnumerateArray().Select(x => x.GetInt32()));
        Assert.Equal("assignment-engine", json.GetProperty("difficultyResolvedBy").GetString());
        Assert.EndsWith("false)", rules.Single(x => x.StartsWith("('hipertension-apnea-v1'")));
    }

    private static Dictionary<string, JsonElement> ReadCriteria()
    {
        var conditions = AddProgramAssignmentCatalog.ReadSeedScript().Split("ON CONFLICT")[0];
        return Regex.Matches(conditions, @"^\('(?<code>[^']+)',.*?'(?<criteria>\{.*\})'::jsonb,", RegexOptions.Multiline)
            .ToDictionary(x => x.Groups["code"].Value,
                x => JsonSerializer.Deserialize<JsonElement>(x.Groups["criteria"].Value));
    }
}
