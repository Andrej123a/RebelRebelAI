using Rebel.Domain.Enums;
using Rebel.Web.Services;
using Xunit;

namespace Rebel.Web.Tests;

public sealed class MenuPriceIntentParserTests
{
    [Theory]
    [InlineData("beer around 320 MKD")]
    [InlineData("beer around 320")]
    public void Parse_AroundPriceCapturesNearestPriceTarget(string query)
    {
        var intent = MenuPriceIntentParser.Parse(query);

        Assert.Equal(320m, intent.Target);
        Assert.True(intent.HasPreference);
    }

    [Fact]
    public void Parse_RangeNormalizesReversedBounds()
    {
        var intent = MenuPriceIntentParser.Parse("food from 400 to 250 denars");

        Assert.Equal(250m, intent.Minimum);
        Assert.Equal(400m, intent.Maximum);
    }

    [Fact]
    public void Parse_MenuSizedRangeWorksWithoutCurrency()
    {
        var intent = MenuPriceIntentParser.Parse("beer 250-400");

        Assert.Equal(250m, intent.Minimum);
        Assert.Equal(400m, intent.Maximum);
    }

    [Fact]
    public void Parse_SmallUnitlessNumberIsNotMistakenForPrice()
    {
        var intent = MenuPriceIntentParser.Parse("beer under 5");

        Assert.False(intent.HasPreference);
    }

    [Theory]
    [InlineData("$", "budget")]
    [InlineData("$$", "mid-range")]
    [InlineData("$$$", "premium")]
    public void Parse_SymbolicTierUsesStableName(string query, string expected)
    {
        var intent = MenuPriceIntentParser.Parse(query);

        Assert.Equal(expected, intent.Tier);
    }

    [Fact]
    public void Bounds_UsesSeparateBeerAndFoodBands()
    {
        var intent = MenuPriceIntentParser.Parse("$$");

        Assert.Equal((301m, 450m), intent.Bounds(CategoryType.Beer));
        Assert.Equal((251m, 400m), intent.Bounds(CategoryType.Food));
    }
}
