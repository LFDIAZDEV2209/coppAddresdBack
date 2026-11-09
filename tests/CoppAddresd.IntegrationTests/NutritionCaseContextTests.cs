using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using CoppAddresd.Infrastructure.Repositories;

namespace CoppAddresd.IntegrationTests;

// El contexto clínico que alimenta el perfil medido de Nutrición.
public sealed partial class NutritionPlanWorkflowTests
{
    /// <summary>
    /// El puntero de batería debe apuntar a la batería COMPLETA más reciente
    /// (9 instrumentos en el mismo grupo, todos completados). Un re-test
    /// puntual posterior no ensombrece el baseline del perfil.
    /// </summary>
    [NutritionPostgresFact]
    public async Task Context_ProfileBatterySticksToLatestCompleteBaseline_NotLaterRetest()
    {
        await using var db = Context();
        var patient = await PatientAsync(db);

        // Batería completa: 9 instrumentos en UN mismo grupo, todos completados.
        var baselineGroup = new HealthTestBatteryAssignment
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Battery = new HealthTestBattery
            {
                Id = Guid.NewGuid(),
                Code = Guid.NewGuid().ToString("N"),
                Name = "Batería inicial completa",
            },
            Status = HealthTestAssignmentStatus.completed,
            AssignedAt = DateTime.UtcNow.AddDays(-14),
        };
        for (var index = 0; index < 9; index++)
        {
            var instrument = new HealthTestInstrument
            {
                Id = Guid.NewGuid(),
                Code = Guid.NewGuid().ToString("N"),
                Name = $"Instrumento {index}",
            };
            var version = new HealthTestVersion
            {
                Id = Guid.NewGuid(),
                Instrument = instrument,
                VersionNumber = 1,
            };
            ((HealthTestBattery)baselineGroup.Battery!).Items.Add(
                new HealthTestBatteryItem
                {
                    Id = Guid.NewGuid(),
                    Battery = (HealthTestBattery)baselineGroup.Battery!,
                    Instrument = instrument,
                    Version = version,
                    IsRequired = true,
                }
            );
            var assignment = new HealthTestAssignment
            {
                Id = Guid.NewGuid(),
                PatientId = patient.Id,
                BatteryAssignment = baselineGroup,
                Version = version,
                Status = HealthTestAssignmentStatus.completed,
                AssignedAt = DateTime.UtcNow.AddDays(-14),
            };
            db.HealthTestAssignments.Add(assignment);
            db.HealthTestEvaluations.Add(
                new HealthTestEvaluation
                {
                    Id = Guid.NewGuid(),
                    PatientId = patient.Id,
                    AssignmentId = assignment.Id,
                    VersionId = version.Id,
                    Status = HealthTestEvaluationStatus.completed,
                    CompletedAt = DateTime.UtcNow,
                }
            );
        }

        // Re-test puntual posterior (batería de un solo instrumento): no reemplaza el baseline.
        var retestGroup = new HealthTestBatteryAssignment
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Battery = new HealthTestBattery
            {
                Id = Guid.NewGuid(),
                Code = Guid.NewGuid().ToString("N"),
                Name = "Re-test puntual",
            },
            Status = HealthTestAssignmentStatus.completed,
            AssignedAt = DateTime.UtcNow,
        };
        var retestInstrument = new HealthTestInstrument
        {
            Id = Guid.NewGuid(),
            Code = Guid.NewGuid().ToString("N"),
            Name = "Re-test",
        };
        var retestVersion = new HealthTestVersion
        {
            Id = Guid.NewGuid(),
            Instrument = retestInstrument,
            VersionNumber = 1,
        };
        ((HealthTestBattery)retestGroup.Battery!).Items.Add(
            new HealthTestBatteryItem
            {
                Id = Guid.NewGuid(),
                Battery = (HealthTestBattery)retestGroup.Battery!,
                Instrument = retestInstrument,
                Version = retestVersion,
                IsRequired = true,
            }
        );
        var retestAssignment = new HealthTestAssignment
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            BatteryAssignment = retestGroup,
            Version = retestVersion,
            Status = HealthTestAssignmentStatus.completed,
            AssignedAt = DateTime.UtcNow,
        };
        db.HealthTestAssignments.Add(retestAssignment);
        db.HealthTestEvaluations.Add(
            new HealthTestEvaluation
            {
                Id = Guid.NewGuid(),
                PatientId = patient.Id,
                AssignmentId = retestAssignment.Id,
                VersionId = retestVersion.Id,
                Status = HealthTestEvaluationStatus.completed,
                CompletedAt = DateTime.UtcNow,
            }
        );
        await db.SaveChangesAsync();

        var context = await new NutritionCaseStore(db).GetContextAsync(
            patient.Id,
            CancellationToken.None
        );
        Assert.True(context!.TestsComplete);
        Assert.Equal(baselineGroup.Id, context.BatteryId);
    }
}
