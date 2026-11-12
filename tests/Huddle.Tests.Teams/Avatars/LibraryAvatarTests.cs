using Agency.Huddle.App.Avatars;

namespace Agency.Huddle.Tests.Avatars;

/// <summary>
/// Pins the derived members of <see cref="LibraryAvatar"/>: the accessible name is built from the raw
/// <c>skin_tone</c> text rather than the group, then the descriptive tags, and the group is looked up from that
/// same tone.
/// </summary>
public sealed class LibraryAvatarTests
{
    /// <summary>The accessible name reads the raw skin tone and gender, then each descriptive tag, joined with commas.</summary>
    [Fact]
    public void AccessibleName_KnownTone_UsesRawToneGenderAndTags()
    {
        LibraryAvatar avatar = new("woman-01.png", "fair", "woman", ["long hair", "glasses"]);

        string actual = avatar.AccessibleName;

        Assert.Equal("fair woman, long hair, glasses", actual);
    }

    /// <summary>An avatar whose tone is in no group still gets its raw tone in the accessible name.</summary>
    [Fact]
    public void AccessibleName_UngroupedTone_KeepsRawTone()
    {
        LibraryAvatar avatar = new("olive-man.png", "olive", "man", []);

        string actual = avatar.AccessibleName;

        Assert.Equal("olive man", actual);
    }

    /// <summary>The skin-*, bg-* and gender tags never reach the accessible name, whatever their case.</summary>
    [Fact]
    public void AccessibleName_ExcludesSkinBackgroundAndGenderTags()
    {
        LibraryAvatar avatar = new(
            "a.png",
            "dark",
            "man",
            ["skin-dark", "Bg-Pink", "man", "WOMAN", "beard", "bg-blue", "SKIN-light", "red hair"]);

        string actual = avatar.AccessibleName;

        Assert.Equal("dark man, beard, red hair", actual);
    }

    /// <summary>With no skin tone and no gender the accessible name is just the descriptive tags.</summary>
    [Fact]
    public void AccessibleName_NoSkinOrGender_IsJustTags()
    {
        LibraryAvatar avatar = new("a.png", string.Empty, string.Empty, ["skin-fair", "hat", "vest"]);

        string actual = avatar.AccessibleName;

        Assert.Equal("hat, vest", actual);
    }

    /// <summary>With only one of skin tone and gender, that one word leads the name without a stray space.</summary>
    [Theory]
    [InlineData("fair", "", "fair, hat")]
    [InlineData("", "woman", "woman, hat")]
    public void AccessibleName_OnlySkinOrOnlyGender_LeadsWithThatWord(string skinTone, string gender, string expected)
    {
        LibraryAvatar avatar = new("a.png", skinTone, gender, ["hat"]);

        string actual = avatar.AccessibleName;

        Assert.Equal(expected, actual);
    }

    /// <summary>A record that says nothing descriptive falls back to the file name without its extension.</summary>
    [Theory]
    [InlineData("pixel-avatar_dark_m_01.png")]
    [InlineData("sub/pixel-avatar_dark_m_01.png")]
    public void AccessibleName_NothingDescriptive_FallsBackToFileName(string file)
    {
        LibraryAvatar avatar = new(file, string.Empty, string.Empty, ["skin-dark", "bg-blue", "man"]);

        string actual = avatar.AccessibleName;

        Assert.Equal("pixel-avatar_dark_m_01", actual);
    }

    /// <summary>Blank or white-space parts never leave an empty or dangling piece in the name, and a record with nothing else falls back to the file name.</summary>
    [Theory]
    [InlineData("", "", new[] { "" }, "a")]
    [InlineData("  ", " ", new[] { "  " }, "a")]
    [InlineData("fair", "woman", new[] { "hat", "" }, "fair woman, hat")]
    [InlineData(" fair ", " woman ", new[] { " hat " }, "fair woman, hat")]
    public void AccessibleName_BlankParts_AreLeftOut(string skinTone, string gender, string[] tags, string expected)
    {
        LibraryAvatar avatar = new("a.png", skinTone, gender, tags);

        string actual = avatar.AccessibleName;

        Assert.Equal(expected, actual);
    }

    /// <summary>A tag repeated in another case is read once, in the casing first written.</summary>
    [Fact]
    public void AccessibleName_RepeatedTagIgnoringCase_ReadOnce()
    {
        LibraryAvatar avatar = new("a.png", "fair", "man", ["hat", "Hat", "HAT", "vest"]);

        string actual = avatar.AccessibleName;

        Assert.Equal("fair man, hat, vest", actual);
    }

    /// <summary>A hyphen in a tag is read as a space, so <c>no-glasses</c> is announced as two words.</summary>
    [Fact]
    public void AccessibleName_HyphenatedTag_ReadWithASpace()
    {
        LibraryAvatar avatar = new("a.png", "dark", "man", ["pink top", "no-glasses"]);

        string actual = avatar.AccessibleName;

        Assert.Equal("dark man, pink top, no glasses", actual);
    }

    /// <summary>A bare <c>glasses</c> is dropped when a coloured glasses tag already says it, and kept when nothing else does.</summary>
    [Theory]
    [InlineData(new[] { "short hair", "glasses", "red glasses" }, "fair man, short hair, red glasses")]
    [InlineData(new[] { "red glasses", "GLASSES" }, "fair man, red glasses")]
    [InlineData(new[] { "short hair", "glasses" }, "fair man, short hair, glasses")]
    [InlineData(new[] { "glasses", "no-glasses" }, "fair man, glasses, no glasses")]
    public void AccessibleName_BareGlasses_DroppedOnlyWhenAColourSaysIt(string[] tags, string expected)
    {
        LibraryAvatar avatar = new("a.png", "fair", "man", tags);

        string actual = avatar.AccessibleName;

        Assert.Equal(expected, actual);
    }

    /// <summary>A fair or light tone is in the Light group.</summary>
    [Theory]
    [InlineData("fair", "Light")]
    [InlineData("light", "Light")]
    public void Group_LightTone_IsLight(string skinTone, string expectedGroup)
    {
        LibraryAvatar avatar = new("a.png", skinTone, "woman", []);

        SkinGroup? actual = avatar.Group;

        Assert.Equal(expectedGroup, actual?.ToString());
    }

    /// <summary>A tone outside the documented set has no group.</summary>
    [Fact]
    public void Group_UngroupedTone_IsNull()
    {
        LibraryAvatar avatar = new("a.png", "olive", "man", []);

        SkinGroup? actual = avatar.Group;

        Assert.Null(actual);
    }
}
