using System.Text.Json;
using CoppAddresd.Application.Features.Nutrition.Cases;
using CoppAddresd.Application.Features.Nutrition.Queries;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Nutrition;

public sealed class NutritionFoodCatalogTests
{
    private static NutritionActor Actor => new(Guid.NewGuid(),null,false,true,false);

    [Fact]
    public async Task Catalog_PreservesDeclaredNutrients_SortsCodes_AndHasStablePickerContract()
    {
        var repository = Substitute.For<INutritionPrescriptionRepository>();
        var foods = NutritionMealSolverTests.Foods();
        repository.ListFoodsAsync(Arg.Any<CancellationToken>()).Returns(new[]{foods["panint"],foods["avena"]});
        var actual = await new NutritionFoodCatalogQueryHandler(repository).Handle(new(Actor),CancellationToken.None);
        Assert.Equal(new[]{"avena","panint"},actual.Select(x => x.Code));
        var oats = actual[0]; Assert.Equal(379,oats.Kcal100); Assert.Equal(67.7,oats.Carbs100);
        Assert.Equal(13.2,oats.Protein100); Assert.Equal(6.5,oats.Fat100); Assert.Equal(10.1,oats.Fiber100);
        Assert.Null(oats.HouseholdEquivalent);
        Assert.Equal(new NutritionHouseholdEquivalent("rebanada","rebanadas",30,.5),actual[1].HouseholdEquivalent);
        var json = JsonSerializer.Serialize(oats,new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal("{\"code\":\"avena\",\"name\":\"Avena en hojuelas (seca)\",\"group\":\"Carbohidratos\",\"role\":\"C\",\"kcal100\":379,\"carbs100\":67.7,\"protein100\":13.2,\"fat100\":6.5,\"fiber100\":10.1,\"householdEquivalent\":null}",json);
    }

    [Theory]
    [InlineData("C","Carbohidratos")] [InlineData("P","Proteínas")]
    [InlineData("F","Grasas")] [InlineData("V","Vegetales")]
    public async Task Catalog_UsesTheFourEngineRoles(string role,string group)
    {
        var repository = Substitute.For<INutritionPrescriptionRepository>();
        repository.ListFoodsAsync(Arg.Any<CancellationToken>()).Returns(new[]{new NutritionFoodItem { Id="prueba",Name="Prueba",FoodGroup=role }});
        var dto = Assert.Single(await new NutritionFoodCatalogQueryHandler(repository).Handle(new(Actor),CancellationToken.None));
        Assert.Equal(role,dto.Role); Assert.Equal(group,dto.Group);
    }

    [Fact]
    public async Task Catalog_MissingPermissionOrIdentity_DoesNotReadRepository()
    {
        var repository = Substitute.For<INutritionPrescriptionRepository>(); var handler = new NutritionFoodCatalogQueryHandler(repository);
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new(Actor with { CanManage=false,ViewAll=true }),CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new(Actor with { UserId=Guid.Empty }),CancellationToken.None));
        Assert.Empty(repository.ReceivedCalls());
    }

    [Fact]
    public async Task Catalog_ReviewerWithoutPatientScope_CanReadAllSeeds_AndPropagatesCancellation()
    {
        var repository = Substitute.For<INutritionPrescriptionRepository>(); var handler = new NutritionFoodCatalogQueryHandler(repository);
        repository.ListFoodsAsync(Arg.Any<CancellationToken>()).Returns(NutritionMealSolverTests.Foods().Values.ToArray());
        using var cancel = new CancellationTokenSource(); var actor = Actor with { CanManage=false,CanApprove=true };
        Assert.Equal(35,(await handler.Handle(new(actor),cancel.Token)).Count);
        await repository.Received(1).ListFoodsAsync(cancel.Token);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.Handle(new(actor),cancel.Token));
        Assert.Single(repository.ReceivedCalls());
    }
}
