using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Contracts;

public sealed class ToolActivityLimitsTests
{
    /// <summary>Text shorter than the limit comes back as the same text, unclipped.</summary>
    [Fact]
    public void Clip_UnderTheLimit_ReturnsTheSameText()
    {
        string? result = ToolActivityLimits.Clip("abc", 5, out bool clipped);

        Assert.Equal("abc", result);
        Assert.False(clipped);
    }

    /// <summary>Text exactly at the limit is kept whole: the limit is inclusive.</summary>
    [Fact]
    public void Clip_AtTheLimit_ReturnsTheSameText()
    {
        string? result = ToolActivityLimits.Clip("abcde", 5, out bool clipped);

        Assert.Equal("abcde", result);
        Assert.False(clipped);
    }

    /// <summary>Text over the limit is cut to exactly the limit and reported as clipped.</summary>
    [Fact]
    public void Clip_OverTheLimit_CutsToTheLimit()
    {
        string? result = ToolActivityLimits.Clip("abcdef", 5, out bool clipped);

        Assert.Equal("abcde", result);
        Assert.True(clipped);
    }

    /// <summary>A cut that would fall between the two halves of an emoji is made one character earlier, so the result never holds a lone surrogate.</summary>
    [Fact]
    public void Clip_WouldSplitASurrogatePair_CutsOneEarlier()
    {
        string? result = ToolActivityLimits.Clip("ab\U0001F600cd", 3, out bool clipped);

        Assert.Equal("ab", result);
        Assert.True(clipped);
    }

    /// <summary>A null stays null and is never reported as clipped.</summary>
    [Fact]
    public void Clip_Null_ReturnsNull()
    {
        string? result = ToolActivityLimits.Clip(null, 5, out bool clipped);

        Assert.Null(result);
        Assert.False(clipped);
    }

    /// <summary>A zero limit clips everything, and the empty string is not reported as clipped.</summary>
    [Fact]
    public void Clip_ZeroLimit_ReturnsEmptyAndReportsClipped()
    {
        string? result = ToolActivityLimits.Clip("abc", 0, out bool clipped);

        Assert.Equal(string.Empty, result);
        Assert.True(clipped);
    }

    /// <summary>The three limits are the values the spec fixes, so a change is deliberate.</summary>
    [Fact]
    public void Limits_HaveTheSpecifiedValues()
    {
        Assert.Equal([4096, 1024, 200], [ToolActivityLimits.MaxEditSideLength, ToolActivityLimits.MaxPathLength, ToolActivityLimits.MaxTitleLength]);
    }
}
