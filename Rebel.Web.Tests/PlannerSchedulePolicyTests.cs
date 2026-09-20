using Rebel.Web.Services;
using Xunit;

namespace Rebel.Web.Tests;

public sealed class PlannerSchedulePolicyTests
{
    [Theory]
    [InlineData("2026-09-21", 24)]
    [InlineData("2026-09-25", 25)]
    [InlineData("2026-09-26", 25)]
    public void GetClosingBoundary_UsesServiceDayClosingTime(
        string date,
        int expectedHour)
    {
        var serviceDate = DateTime.Parse(date);

        var result = PlannerSchedulePolicy.GetClosingBoundary(serviceDate);

        Assert.Equal(TimeSpan.FromHours(expectedHour), result);
    }

    [Fact]
    public void ResolveEventEnd_UsesClosingTime_WhenEndIsMissing()
    {
        var friday = new DateTime(2026, 9, 25);

        var result = PlannerSchedulePolicy.ResolveEventEnd(
            friday,
            TimeSpan.FromHours(20),
            null);

        Assert.Equal(TimeSpan.FromHours(25), result);
    }

    [Fact]
    public void ResolveEventEnd_KeepsSameDayEnd()
    {
        var monday = new DateTime(2026, 9, 21);

        var result = PlannerSchedulePolicy.ResolveEventEnd(
            monday,
            TimeSpan.FromHours(20),
            TimeSpan.FromHours(22));

        Assert.Equal(TimeSpan.FromHours(22), result);
    }

    [Fact]
    public void ResolveEventEnd_TreatsOneAmAsNextDayOnSaturday()
    {
        var saturday = new DateTime(2026, 9, 26);

        var result = PlannerSchedulePolicy.ResolveEventEnd(
            saturday,
            TimeSpan.FromHours(20),
            TimeSpan.FromHours(1));

        Assert.Equal(TimeSpan.FromHours(25), result);
    }
}
