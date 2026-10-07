using System.Text.Json;
using CoppAddresd.Application.Features.Inventory;
using CoppAddresd.Domain.Entities;

namespace CoppAddresd.UnitTests.Inventory;

public class ProductDateContractTests
{
    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    public void ProductResponses_ExpirationIsCalendarDate_WithoutTimezoneShift(DateTimeKind kind)
    {
        var product = new Product
        {
            ExpirationDate = new DateTime(2030, 1, 31, 0, 0, 0, kind),
            CreatedAt = new DateTime(2026, 10, 7, 20, 0, 0, DateTimeKind.Utc)
        };
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        using var detail = JsonDocument.Parse(JsonSerializer.Serialize(ProductDto.FromEntity(product), options));
        using var list = JsonDocument.Parse(JsonSerializer.Serialize(ProductListItemDto.FromEntity(product), options));
        Assert.Equal("2030-01-31", detail.RootElement.GetProperty("expirationDate").GetString());
        Assert.Equal("2030-01-31", list.RootElement.GetProperty("expirationDate").GetString());
        Assert.Equal("2026-10-07T20:00:00Z", detail.RootElement.GetProperty("createdAt").GetString());
    }

    [Fact]
    public void ProductResponses_WithoutExpiration_PreserveNull()
    {
        var product = new Product();
        Assert.Null(ProductDto.FromEntity(product).ExpirationDate);
        Assert.Null(ProductListItemDto.FromEntity(product).ExpirationDate);
    }
}
