using Agency.Huddle.App.Agents;

namespace Agency.Huddle.Tests.Agents;

/// <summary>Tests for <see cref="HireLimits"/>.</summary>
public sealed class HireLimitsTests
{
    /// <summary>Hires that land exactly on a limit are allowed.</summary>
    [Fact]
    public void Check_AtLimits_Allowed()
    {
        HireLimitResult result = HireLimits.Check(4, 1, 9, 5, 10);

        Assert.True(result.Allowed);
        Assert.Equal(HireLimitKind.None, result.Kind);
        Assert.Equal(0, result.Limit);
    }

    /// <summary>One hire over the per-Team limit is refused with that limit.</summary>
    [Fact]
    public void Check_OneOverPerTeam_RefusedPerTeam()
    {
        HireLimitResult result = HireLimits.Check(5, 1, 0, 5, 100);

        Assert.False(result.Allowed);
        Assert.Equal(HireLimitKind.PerTeam, result.Kind);
        Assert.Equal(5, result.Limit);
    }

    /// <summary>One hire over the live limit is refused with that limit.</summary>
    [Fact]
    public void Check_OneOverLive_RefusedLive()
    {
        HireLimitResult result = HireLimits.Check(0, 1, 10, 5, 10);

        Assert.False(result.Allowed);
        Assert.Equal(HireLimitKind.LiveProcesses, result.Kind);
        Assert.Equal(10, result.Limit);
    }

    /// <summary>Limits of zero are off, however large the counts.</summary>
    [Fact]
    public void Check_BothLimitsOff_Allowed()
    {
        HireLimitResult result = HireLimits.Check(500, 50, 900, 0, 0);

        Assert.True(result.Allowed);
        Assert.Equal(HireLimitKind.None, result.Kind);
    }

    /// <summary>Negative limits are off like zero.</summary>
    [Fact]
    public void Check_NegativeLimits_TreatedAsOff()
    {
        HireLimitResult result = HireLimits.Check(5, 5, 5, -1, -3);

        Assert.True(result.Allowed);
    }

    /// <summary>When both limits are exceeded the per-Team limit is reported.</summary>
    [Fact]
    public void Check_BothOver_ReportsPerTeam()
    {
        HireLimitResult result = HireLimits.Check(5, 2, 10, 5, 10);

        Assert.False(result.Allowed);
        Assert.Equal(HireLimitKind.PerTeam, result.Kind);
        Assert.Equal(5, result.Limit);
    }

    /// <summary>A batch of three that crosses the line is refused although one would fit.</summary>
    [Fact]
    public void Check_ThreeHiresCrossingLine_Refused()
    {
        HireLimitResult result = HireLimits.Check(3, 3, 0, 5, 0);

        Assert.False(result.Allowed);
        Assert.Equal(HireLimitKind.PerTeam, result.Kind);
    }

    /// <summary>A batch of three that lands exactly on the live limit is allowed.</summary>
    [Fact]
    public void Check_ThreeHiresExactlyLive_Allowed()
    {
        HireLimitResult result = HireLimits.Check(0, 3, 7, 0, 10);

        Assert.True(result.Allowed);
    }

    /// <summary>A batch of three crossing only the live limit reports the live limit.</summary>
    [Fact]
    public void Check_ThreeHiresCrossingLive_RefusedLive()
    {
        HireLimitResult result = HireLimits.Check(0, 3, 8, 0, 10);

        Assert.Equal(HireLimitKind.LiveProcesses, result.Kind);
        Assert.Equal(10, result.Limit);
    }

    /// <summary>A per-Team limit being off lets the live limit refuse.</summary>
    [Fact]
    public void Check_PerTeamOff_LiveStillApplies()
    {
        HireLimitResult result = HireLimits.Check(99, 1, 10, 0, 10);

        Assert.False(result.Allowed);
        Assert.Equal(HireLimitKind.LiveProcesses, result.Kind);
    }
}
