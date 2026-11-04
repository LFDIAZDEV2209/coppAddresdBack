using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.ProgramProgress.Assignment;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Infrastructure.Migrations;

namespace CoppAddresd.UnitTests.ProgramProgress;

public sealed class AssignmentEngineTests
{
    private static readonly DateTime Captured = new(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc);
    private readonly AssignmentEngine _engine = new();

    internal static AssignmentCatalog Catalog()
    {
        var sql = AddProgramAssignmentCatalog.ReadSeedScript();
        var sections = sql.Split("INSERT INTO app.program_assignment_rules");
        var conditions = sections[0].Split('\n').Select(x => x.Trim()).Where(x => x.StartsWith("('")).Select(line =>
        {
            var code = Regex.Match(line, @"^\('([^']+)'").Groups[1].Value;
            var criteria = Regex.Match(line, @"'(\{[^']+\})'::jsonb").Groups[1].Value;
            return new ProgramCondition { Code = code, Criteria = criteria, SourceRef = "Fuente " + code,
                IsActive = !new[] { "phq-9", "gad-7" }.Contains(code) };
        }).ToArray();
        var rules = sections[1].Split('\n').Select(x => x.Trim()).Where(x => x.StartsWith("('")).Select(line =>
        {
            var head = Regex.Match(line, @"^\('([^']+)',(?:'([^']+)'|NULL)");
            var documents = Regex.Matches(line, @"'(\{[^']+\})'::jsonb");
            var tail = Regex.Match(line, @",(\d+),(\d+),(true|false)\),?$");
            return new ProgramAssignmentRule { Id = Guid.NewGuid(), Code = head.Groups[1].Value,
                ConditionCode = head.Groups[2].Success ? head.Groups[2].Value : null,
                Criteria = documents[0].Groups[1].Value, Target = documents[1].Groups[1].Value,
                Priority = int.Parse(tail.Groups[1].Value), Weight = int.Parse(tail.Groups[2].Value),
                IsActive = tail.Groups[3].Value == "true" };
        }).ToArray();
        return new(conditions, rules,
            new[] { "Plan Obesidad — Déficit calórico", "Plan Diabetes Tipo 2", "Plan Hipertensión — DASH" }
                .Select(n => new AssignmentContent(Guid.NewGuid(), n)).ToArray(),
            new[] { ("Caminata Activa", 1), ("Flexibilidad y Movilidad", 1), ("Full Body", 2), ("Cardio HIIT", 3) }
                .Select(n => new AssignmentContent(Guid.NewGuid(), n.Item1, n.Item2)).ToArray());
    }

    private static AssignmentProfileData Profile(params AssignmentBiometric[] biometrics) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Femenino", null, [], biometrics);
    private static AssignmentBiometric Bio(string code, decimal value, string unit) => new(Guid.NewGuid(), code, value, unit, null, null);

    [Theory]
    [InlineData(29.99, false)]
    [InlineData(30, true)]
    [InlineData(35, true)]
    public void Obesidad_UmbralRealYSnapshotTrazable(decimal bmi, bool expected)
    {
        var catalog = Catalog();
        var decision = _engine.Build(Profile(Bio("bmi", bmi, "kg/m²")), catalog, new(), Captured);
        var snapshot = JsonNode.Parse(decision.ProfileSnapshot)!;
        Assert.Equal(expected, snapshot["facts"]!["conditions.obesidad.active"]!.GetValue<bool>());
        Assert.Equal(expected, JsonNode.Parse(decision.Proposal)!["nutritionPlan"] is not null);
        Assert.Contains("Fuente obesidad", decision.ProfileSnapshot);
        Assert.Contains("questionCode", decision.ProfileSnapshot);
    }

    [Fact]
    public void DatosFaltantesOFueraDeRango_NoInventanCondiciones()
    {
        var decision = _engine.Build(Profile(new AssignmentBiometric(Guid.NewGuid(), "bmi", 60, "kg/m²", 10, 50)), Catalog(), new(), Captured);
        var snapshot = JsonNode.Parse(decision.ProfileSnapshot)!;
        Assert.False(snapshot["facts"]!["conditions.obesidad.active"]!.GetValue<bool>());
        Assert.False(snapshot["facts"]!["conditions.diabetes-t2.active"]!.GetValue<bool>());
        Assert.False(snapshot["biometrics"]![0]!["valid"]!.GetValue<bool>());
    }

    [Fact]
    public void PesoYTalla_ReconstruyenImcConUnidadesReales()
    {
        var decision = _engine.Build(Profile(Bio("peso", 100, "kg"), Bio("talla", 165, "cm")), Catalog(), new(), Captured);
        Assert.Equal(36.73m, JsonNode.Parse(decision.ProfileSnapshot)!["facts"]!["bmi"]!.GetValue<decimal>());
    }

    [Theory]
    [InlineData(20, "Caminata Activa")]
    [InlineData(50, "Full Body")]
    [InlineData(90, "Cardio HIIT")]
    public void Movimiento_TerciosOperativosResuelvenNombres(decimal score, string routine)
    {
        var data = Profile() with { Results = [new(Guid.NewGuid(), "movimiento", "subscale", "capacidad", score, null, null, 100)] };
        var decision = _engine.Build(data, Catalog(), new(), Captured);
        var proposal = JsonNode.Parse(decision.Proposal)!;
        Assert.Equal(routine, proposal["routine"]!["name"]!.GetValue<string>());
        Assert.NotNull(proposal["matchedRules"]![0]!["ruleId"]);
    }

    [Fact]
    public void MismoSnapshot_ProduceMismaPropuestaAunqueCambieOrdenDelCatalogo()
    {
        var catalog = Catalog();
        var snapshot = _engine.BuildSnapshot(Profile(Bio("bmi", 35, "kg/m²"), Bio("systolic", 150, "mmHg")), catalog, Captured);
        var first = _engine.Evaluate(snapshot, catalog, new()).ToJsonString();
        var second = _engine.Evaluate(snapshot, catalog with { Rules = catalog.Rules.Reverse().ToArray() }, new()).ToJsonString();
        Assert.Equal(first, second);
        Assert.Equal("Plan Hipertensión — DASH", JsonNode.Parse(first)!["nutritionPlan"]!["name"]!.GetValue<string>());
    }

    [Fact]
    public void ReglaNuevaPorDatos_PesoYDesempatePorCodigoCambianLaPropuesta()
    {
        var catalog = Catalog();
        var a = new ProgramAssignmentRule { Code = "a", Criteria = "{}", Priority = 999, Weight = 2,
            Target = """{"routineName":"Flexibilidad y Movilidad"}""" };
        var z = new ProgramAssignmentRule { Code = "z", Criteria = "{}", Priority = 999, Weight = 2,
            Target = """{"routineName":"Cardio HIIT"}""" };
        var lowerWeight = new ProgramAssignmentRule { Code = "0", Criteria = "{}", Priority = 999, Weight = 1,
            Target = """{"routineName":"Cardio HIIT"}""" };
        catalog = catalog with { Rules = [z, lowerWeight, a] };
        var proposal = _engine.Build(Profile(), catalog, new(), Captured).Proposal;
        Assert.Equal("Flexibilidad y Movilidad", JsonNode.Parse(proposal)!["routine"]!["name"]!.GetValue<string>());
    }

    [Fact]
    public void RangoSubescala_NoCoincideSinValorYRespetaLimites()
    {
        var catalog = Catalog() with { Rules = [new ProgramAssignmentRule { Code = "range",
            Criteria = """{"subscaleRanges":[{"testCode":"movimiento","code":"capacidad","min":3,"max":5}]}""",
            Target = """{"routineName":"Full Body"}""" }] };
        Assert.Null(JsonNode.Parse(_engine.Build(Profile(), catalog, new(), Captured).Proposal)!["routine"]);
        var profile = Profile() with { Results = [new(Guid.NewGuid(), "movimiento", "subscale", "capacidad", 3, null, null, 5)] };
        Assert.NotNull(JsonNode.Parse(_engine.Build(profile, catalog, new(), Captured).Proposal)!["routine"]);
    }

    [Fact]
    public void NombreInexistente_OEscalaPhq_SoloConservaDefaultYReferencia()
    {
        var catalog = Catalog() with { Rules = [new ProgramAssignmentRule { Code = "missing", Criteria = "{}",
            Target = """{"routineName":"No existe"}""" }] };
        var data = Profile() with { Results = [new(Guid.NewGuid(), "phq-9", "score", "phq-9", 20, "high", null, 27)] };
        var decision = _engine.Build(data, catalog, new(), Captured);
        Assert.True(JsonNode.Parse(decision.Proposal)!["useTemplateRoutineDefault"]!.GetValue<bool>());
        Assert.Equal("severe", JsonNode.Parse(decision.ProfileSnapshot)!["facts"]!["conditions.phq-9.severity"]!.GetValue<string>());
        Assert.False(JsonNode.Parse(decision.ProfileSnapshot)!["facts"]!["conditions.phq-9.active"]!.GetValue<bool>());
    }
}
