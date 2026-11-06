using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.FoodAi;
using CoppAddresd.Infrastructure.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CoppAddresd.UnitTests.Localization;

public sealed class AuditMigrationTargetModelTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void AuditTargetModels_KeepMergedReservationSchema_AndOnlyTheirChronologicalColumns(int stage)
    {
        Migration migration = stage switch
        {
            0 => new AddFoodAnalysisEnglishNames(),
            1 => new AddDeviceTokenLanguage(),
            _ => new AddNutritionInstructionTranslationProvenance(),
        };
        var model = migration.TargetModel;
        var reservation = model.FindEntityType(typeof(NutritionDraftReservation).FullName!)!;
        Assert.NotNull(reservation);
        Assert.Equal("nutrition_draft_reservations", reservation.GetTableName());
        Assert.Equal("app", reservation.GetSchema());
        Assert.Equal(20, reservation.FindProperty(nameof(NutritionDraftReservation.State))!.GetMaxLength());
        Assert.Contains(reservation.GetIndexes(), index => index.IsUnique
            && index.Properties.Select(property => property.Name).SequenceEqual(new[] { "PatientId", "BatteryAssignmentId" }));
        Assert.Contains(reservation.GetForeignKeys(), key => key.PrincipalEntityType.Name == typeof(PatientProfile).FullName
            && key.Properties.Single().Name == "PatientId");
        Assert.NotNull(model.FindEntityType(typeof(FoodAnalysisItem).FullName!)!.FindProperty(nameof(FoodAnalysisItem.NameEn)));
        Assert.Equal(stage >= 1, model.FindEntityType(typeof(DeviceToken).FullName!)!.FindProperty(nameof(DeviceToken.Language)) is not null);
        Assert.Equal(stage == 2, model.FindEntityType(typeof(NutritionPrescription).FullName!)!
            .FindProperty(nameof(NutritionPrescription.InstructionsEnSourceHash)) is not null);
    }
}
