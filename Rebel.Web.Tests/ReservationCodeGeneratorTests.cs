using System.Text.RegularExpressions;
using Rebel.Web.Services;
using Xunit;

namespace Rebel.Web.Tests;

public sealed class ReservationCodeGeneratorTests
{
    [Fact]
    public void Create_UsesExpectedPublicFormat()
    {
        var code = ReservationCodeGenerator.Create();

        Assert.Matches(new Regex("^RR-[0-9A-F]{10}$"), code);
    }

    [Fact]
    public void Create_ProducesUniqueCodesAcrossSample()
    {
        var codes = Enumerable.Range(0, 1_000)
            .Select(_ => ReservationCodeGenerator.Create())
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(1_000, codes.Count);
    }
}
