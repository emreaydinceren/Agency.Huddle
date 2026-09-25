using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Tests for <see cref="LibrarySegments.Refusal(string)"/>: the single per-segment boundary check
/// shared by <see cref="LibraryPathResolver"/>'s step 2 and <c>LibraryNames.Validate</c>, so the two
/// can never drift (corrections-B3 Addendum A1). Every rule in corrections-B3 4.2.t item 16 behaves
/// identically on every OS except the 8.3-alias row (item 23), which is Windows-only.
/// </summary>
public sealed class LibrarySegmentsTests
{
    /// <summary>Every step-2 rule (item 16) refuses the segment it targets; an ordinary segment is accepted.</summary>
    [Theory]
    [InlineData("", true)] // empty segment, e.g. from "Marketing//plan.md" or "//x"
    [InlineData(".", true)] // "./Marketing"
    [InlineData("..", true)] // "Marketing/../../x"
    [InlineData("...", true)] // "Marketing/.../x" - ends with '.'
    [InlineData("Marketing.", true)] // "Marketing./x" - ends with '.'
    [InlineData("Marketing ", true)] // "Marketing /x" - ends with space
    [InlineData("C:", true)] // "C:\x", "C:x" - contains ':'
    [InlineData("C:x", true)] // "C:x" as a single segment - contains ':'
    [InlineData("a.md:stream", true)] // contains ':'
    [InlineData("?", true)] // "\\?\C:\x" - contains '?'
    [InlineData("<x", true)] // contains '<'
    [InlineData("x>", true)] // contains '>'
    [InlineData("\"x", true)] // contains '"'
    [InlineData("a|b", true)] // contains '|'
    [InlineData("a*b", true)] // contains '*'
    [InlineData("a\u0001b", true)] // control char
    [InlineData("CON", true)] // "Marketing/CON" - device name
    [InlineData("con", true)] // device name, case-insensitive
    [InlineData("NUL.md", true)] // "nul.md" - device name with extension
    [InlineData("nul.md", true)] // device name with extension, lower case
    [InlineData("AUX", true)] // device name
    [InlineData("PRN", true)] // device name
    [InlineData("COM1", true)] // device name with digit
    [InlineData("LPT9", true)] // device name with digit
    [InlineData("Marketing", false)]
    [InlineData("Launch Q4", false)]
    [InlineData("plan.md", false)]
    [InlineData("CONTACT", false)] // not literally a device name
    [InlineData("readme.md", false)]
    public void Refusal_Table(string segment, bool shouldRefuse)
    {
        string? refusal = LibrarySegments.Refusal(segment);

        if (shouldRefuse)
        {
            Assert.NotNull(refusal);
        }
        else
        {
            Assert.Null(refusal);
        }
    }

    /// <summary>Windows-only: an 8.3 alias-shaped segment (e.g. <c>ROOM-S~1</c>) is refused (corrections-B3 item 23).</summary>
    [Fact]
    public void Refusal_EightDotThreeAlias_RefusedOnWindowsOnly()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("8.3-alias refusal is Windows-only (corrections-B3 item 23).");
            return;
        }

        Assert.NotNull(LibrarySegments.Refusal("ROOM-S~1"));
    }

    /// <summary>Off Windows, an 8.3 alias-shaped segment is an ordinary segment name (corrections-B3 item 23).</summary>
    [Fact]
    public void Refusal_EightDotThreeAlias_AcceptedOffWindows()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("The Windows counterpart of Refusal_EightDotThreeAlias_RefusedOnWindowsOnly covers this OS.");
            return;
        }

        Assert.Null(LibrarySegments.Refusal("ROOM-S~1"));
    }
}
