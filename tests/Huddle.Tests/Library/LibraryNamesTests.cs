namespace Agency.Huddle.App.Library;

/// <summary>Tests for <see cref="LibraryNames.Validate"/>.</summary>
public sealed class LibraryNamesTests
{
    /// <summary>A name is validated against the rules in Spec §6.4.</summary>
    [Theory]
    [InlineData("plan.md", null)]
    [InlineData("Launch Q4", null)]
    [InlineData("", "A name can't be empty.")]
    [InlineData("   ", "A name can't be empty.")]
    [InlineData(".", "A name can't be empty.")]
    [InlineData("..", "A name can't be empty.")]
    [InlineData("a/b", "A name can't contain /.")]
    [InlineData("a\\b", "A name can't contain \\.")]
    [InlineData("notes.", "A name can't end with a dot or a space.")]
    [InlineData("notes ", "A name can't end with a dot or a space.")]
    [InlineData("CON", "\"CON\" is reserved by Windows.")]
    [InlineData("con.md", "\"con.md\" is reserved by Windows.")]
    [InlineData("NUL", "\"NUL\" is reserved by Windows.")]
    [InlineData("COM1", "\"COM1\" is reserved by Windows.")]
    [InlineData("LPT9.txt", "\"LPT9.txt\" is reserved by Windows.")]
    public void Validate_NameAgainstRules(string name, string? expectedError)
    {
        string? error = LibraryNames.Validate(name);
        Assert.Equal(expectedError, error);
    }

    /// <summary>Explicit invalid file name characters are refused on every OS.</summary>
    [Theory]
    [InlineData("a:b", "A name can't contain :.")]
    [InlineData("a*b", "A name can't contain *.")]
    [InlineData("a?b", "A name can't contain ?.")]
    [InlineData("a\"b", "A name can't contain \".")]
    [InlineData("a<b", "A name can't contain <.")]
    [InlineData("a>b", "A name can't contain >.")]
    [InlineData("a|b", "A name can't contain |.")]
    public void Validate_InvalidCharsEveryOs(string name, string? expectedError)
    {
        string? error = LibraryNames.Validate(name);
        Assert.Equal(expectedError, error);
    }

    /// <summary>Control characters are refused.</summary>
    [Fact]
    public void Validate_ControlChar_Refused()
    {
        string name = "note\x00end";
        string? error = LibraryNames.Validate(name);
        Assert.NotNull(error);
    }
}
