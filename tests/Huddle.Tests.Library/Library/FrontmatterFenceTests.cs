using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>Pins <see cref="FrontmatterFence"/>: the one lenient rule every frontmatter reader shares for where a block opens and closes.</summary>
public sealed class FrontmatterFenceTests
{
    /// <summary>A first line opens a block when, after one leading BOM and surrounding whitespace, it is exactly <c>---</c>.</summary>
    /// <param name="line">The line.</param>
    [Theory]
    [InlineData("---")]
    [InlineData("\uFEFF---")]
    [InlineData("  ---  ")]
    [InlineData("\t---\t")]
    [InlineData("---\r")]
    [InlineData("\uFEFF  ---  ")]
    public void IsOpening_Fence_IsTrue(string line)
    {
        Assert.True(FrontmatterFence.IsOpening(line));
        Assert.True(FrontmatterFence.IsOpening(line.AsSpan()));
    }

    /// <summary>Anything else is not an opening fence, including the closing-only <c>...</c>, a longer run of dashes, and two BOMs.</summary>
    /// <param name="line">The line.</param>
    [Theory]
    [InlineData("")]
    [InlineData("--")]
    [InlineData("----")]
    [InlineData("--- x")]
    [InlineData("- --")]
    [InlineData("...")]
    [InlineData("\uFEFF\uFEFF---")]
    [InlineData("---:")]
    public void IsOpening_NotAFence_IsFalse(string line)
    {
        Assert.False(FrontmatterFence.IsOpening(line));
        Assert.False(FrontmatterFence.IsOpening(line.AsSpan()));
    }

    /// <summary>A line closes a block when it trims to exactly <c>---</c> or <c>...</c>.</summary>
    /// <param name="line">The line.</param>
    [Theory]
    [InlineData("---")]
    [InlineData("...")]
    [InlineData("  ---  ")]
    [InlineData("  ...  ")]
    [InlineData("...\r")]
    public void IsClosing_Fence_IsTrue(string line)
    {
        Assert.True(FrontmatterFence.IsClosing(line));
        Assert.True(FrontmatterFence.IsClosing(line.AsSpan()));
    }

    /// <summary>Anything else is not a closing fence, and a BOM is only ever tolerated on the opening line.</summary>
    /// <param name="line">The line.</param>
    [Theory]
    [InlineData("")]
    [InlineData("....")]
    [InlineData("..")]
    [InlineData("----")]
    [InlineData("--- x")]
    [InlineData("\uFEFF---")]
    [InlineData("a: ---")]
    public void IsClosing_NotAFence_IsFalse(string line)
    {
        Assert.False(FrontmatterFence.IsClosing(line));
        Assert.False(FrontmatterFence.IsClosing(line.AsSpan()));
    }

    /// <summary>The first closing line at or after the start index wins, whatever its spelling.</summary>
    [Fact]
    public void FindClosing_ReturnsTheFirstClosingLineFromTheStartIndex()
    {
        string[] lines = ["---", "a: 1", "  ...  ", "b: 2", "---"];

        Assert.Equal(2, FrontmatterFence.FindClosing(lines, 1));
        Assert.Equal(4, FrontmatterFence.FindClosing(lines, 3));
        Assert.Equal(0, FrontmatterFence.FindClosing(lines, 0));
    }

    /// <summary>With no closing line from the start index on, or a start index past the end, there is no close.</summary>
    [Fact]
    public void FindClosing_NoClosingLine_IsMinusOne()
    {
        string[] lines = ["---", "a: 1", "--- x", "...."];

        Assert.Equal(-1, FrontmatterFence.FindClosing(lines, 1));
        Assert.Equal(-1, FrontmatterFence.FindClosing(lines, 4));
        Assert.Equal(-1, FrontmatterFence.FindClosing([], 0));
    }
}
