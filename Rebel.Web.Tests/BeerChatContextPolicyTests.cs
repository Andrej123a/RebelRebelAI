using Rebel.Web.Services;
using Xunit;

namespace Rebel.Web.Tests;

public sealed class BeerChatContextPolicyTests
{
    [Theory]
    [InlineData("compare the first two")]
    [InlineData("which one is cheaper?")]
    [InlineData("which is less bitter?")]
    [InlineData("which of these is strongest?")]
    [InlineData("between those beers, what is sweeter?")]
    [InlineData("show me the cheaper of the two")]
    public void RefersToPreviousResults_AcceptsReferentialFollowUps(string message) =>
        Assert.True(BeerChatContextPolicy.RefersToPreviousResults(message));

    [Theory]
    [InlineData("cheapest beer")]
    [InlineData("give me the most expensive beer in the fridge")]
    [InlineData("show me three cheaper beers")]
    [InlineData("a less bitter IPA")]
    [InlineData("strongest available beer")]
    [InlineData("what is the cheapest lager?")]
    public void RefersToPreviousResults_RejectsGlobalOrRefinementRequests(string message) =>
        Assert.False(BeerChatContextPolicy.RefersToPreviousResults(message));
}
