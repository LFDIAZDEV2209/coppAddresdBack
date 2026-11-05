using CoppAddresd.Application.Features.Telemedicine;

namespace CoppAddresd.UnitTests.Features.Telemedicine;

public sealed class AppointmentReferenceFormattingTests
{
    [Fact]
    public void FullName_OmitsMissingMiddleNameWithoutDoubleSpaces()
    {
        var name = AppointmentReferenceFormatting.FullName("Evelyn", null, "Rivera");

        Assert.Equal("Evelyn Rivera", name);
    }

    [Fact]
    public void FullName_TrimsAndOmitsBlankParts()
    {
        var name = AppointmentReferenceFormatting.FullName("  Evelyn ", "  ", " Rivera ");

        Assert.Equal("Evelyn Rivera", name);
    }
}
