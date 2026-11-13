using System.Text.Json;
using CoppAddresd.Application.Features.ProgramProgress.Commands.BulkAssignProgramContent;

namespace CoppAddresd.UnitTests.ProgramProgress;

public sealed class WeekSnapshotJsonTests
{
    [Fact]
    public void AntiguoSinMetadatos_ConservaContenidoYNullsEnIdaYVuelta()
    {
        var snapshot = JsonSerializer.Deserialize<JsonElement>("""[{"weekday":4,"task_code":"ejercicio","points":20,"sort_order":2}]""");
        var row = Assert.Single(WeekSnapshotJson.Parse(snapshot));
        Assert.Equal(new WeekSnapshotTaskRow(4, "ejercicio", 20, 2), row);
        Assert.Equal(row, Assert.Single(WeekSnapshotJson.Parse(WeekSnapshotJson.Serialize([row]))));
    }

    [Theory]
    [InlineData("program", false)]
    [InlineData("individual", true)]
    public void Nuevo_ConservaTodosLosMetadatos(string origin, bool rest)
    {
        var row = new WeekSnapshotTaskRow(4, "ejercicio", 20, 2, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), origin, rest, Guid.NewGuid(), DateTimeOffset.Parse("2026-11-13T10:30:00-05:00"), 1);
        var snapshot = WeekSnapshotJson.Serialize([row]);
        Assert.Equal(row, Assert.Single(WeekSnapshotJson.Parse(snapshot)));
        Assert.Equal(origin, snapshot[0].GetProperty("origin").GetString());
        Assert.Equal(rest, snapshot[0].GetProperty("rest").GetBoolean());
        Assert.Equal(1, snapshot[0].GetProperty("backfill_version").GetInt32());
        // Las escrituras selectivas existentes con `with` conservan el origen y la auditoría.
        var edited = row with { MediaId = Guid.NewGuid() };
        Assert.Equal(edited, Assert.Single(WeekSnapshotJson.Parse(WeekSnapshotJson.Serialize([edited]))));
    }

    [Fact]
    public void MetadatosNulosOInvalidos_SeLeenSinRomperContenidoAntiguo()
    {
        var snapshot = JsonSerializer.Deserialize<JsonElement>("""[{"weekday":1,"task_code":"podcast","points":10,"sort_order":1,"origin":null,"rest":"no","edited_by":"invalido","edited_at":"invalido","backfill_version":"v?"}]""");
        Assert.Equal(new WeekSnapshotTaskRow(1, "podcast", 10, 1), Assert.Single(WeekSnapshotJson.Parse(snapshot)));
    }
}
