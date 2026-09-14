using CoppAddresd.Auth.Security;

namespace CoppAddresd.UnitTests.Auth.Services;

public sealed class ErpSessionVersionTests
{
    [Theory]
    [InlineData(null, 0, true)]
    [InlineData(null, 1, false)]
    [InlineData("0", 0, true)]
    [InlineData("0", 2, false)]
    [InlineData("1", 2, false)]
    [InlineData("2", 2, true)]
    [InlineData("3", 2, false)]
    [InlineData("", 0, false)]
    [InlineData("-1", 0, false)]
    [InlineData("invalid", 0, false)]
    [InlineData("9223372036854775808", 0, false)]
    public void Matches_CompatibilidadYRevocacion_NoReviveSesionesPrevias(
        string? claim,
        long version,
        bool expected
    )
    {
        Assert.Equal(expected, ErpSessionVersion.Matches(claim, version));
    }
}
