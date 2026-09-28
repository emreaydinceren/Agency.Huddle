using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>Tests for Library file and document records (Spec §6.4, §6.11).</summary>
public sealed class LibraryFileRecordsTests
{
    /// <summary>LibraryFileKind has exactly Markdown, Text, Image, Svg, Other members.</summary>
    [Fact]
    public void LibraryFileKind_Members_MatchTypeMap()
    {
        string[] expected = ["Markdown", "Text", "Image", "Svg", "Other"];

        Assert.Equal(expected, Enum.GetNames<LibraryFileKind>());
    }

    /// <summary>LineEnding has exactly CrLf and Lf members.</summary>
    [Fact]
    public void LineEnding_Members_AreCrLfLf()
    {
        string[] expected = ["CrLf", "Lf"];

        Assert.Equal(expected, Enum.GetNames<LineEnding>());
    }

    /// <summary>TextFileFormat equality is by value, not by reference.</summary>
    [Fact]
    public void TextFileFormat_Equality_IsByValue()
    {
        TextFileFormat a = new("utf-8", false, LineEnding.Lf, false);
        TextFileFormat b = new("utf-8", false, LineEnding.Lf, false);

        Assert.Equal(a, b);
    }

    /// <summary>LibraryEntry equality is by value, not by reference.</summary>
    [Fact]
    public void LibraryEntry_Equality_IsByValue()
    {
        LibraryRoot root = new("id", "Name", "path", LibraryRootKind.Teams);
        LibraryPath path = new(root, "a", "full/a", LibraryNodeRole.File);
        DateTimeOffset utc = new(2026, 9, 25, 11, 36, 0, TimeSpan.Zero);

        LibraryEntry a = new(path, false, 100, utc);
        LibraryEntry b = new(path, false, 100, utc);

        Assert.Equal(a, b);
    }

    /// <summary>LibraryResult.Ok() sets Succeeded true; Fail() sets it false.</summary>
    [Fact]
    public void LibraryResult_ErrorNull_IsSuccess()
    {
        LibraryResult<string> ok = LibraryResult<string>.Ok("value");
        LibraryResult<string> fail = LibraryResult<string>.Fail("error");

        Assert.True(ok.Succeeded);
        Assert.Equal("value", ok.Value);
        Assert.False(fail.Succeeded);
        Assert.Equal("error", fail.Error);
    }

    /// <summary>LibraryResult.Fail() guards error with ThrowIfNullOrEmpty.</summary>
    [Fact]
    public void LibraryResult_FailNullOrEmpty_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => LibraryResult<string>.Fail(null!));
        Assert.Throws<ArgumentException>(() => LibraryResult<string>.Fail(""));
    }
}
