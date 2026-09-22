using Agency.Huddle.App.Avatars;

namespace Agency.Huddle.Tests.Avatars;

/// <summary>
/// Pins <see cref="Avatar.TrimLabel(string?)"/>'s text-element counting - the part easy to get wrong,
/// because a naive <see cref="string.Length"/> or <see cref="System.Text.Rune"/> count both overcount a
/// multi-codepoint emoji - and <see cref="Avatar.IsDefault"/>'s "all three fields unset" reading.
/// </summary>
public sealed class AvatarTests
{
    /// <summary>A three-character label passes through unchanged.</summary>
    [Fact]
    public void TrimLabel_ThreeCharacters_PassesUnchanged()
    {
        var trimmed = Avatar.TrimLabel("ABC");

        Assert.Equal("ABC", trimmed);
    }

    /// <summary>A four-character label truncates to its first three text elements.</summary>
    [Fact]
    public void TrimLabel_FourCharacters_TruncatesToThree()
    {
        var trimmed = Avatar.TrimLabel("ABCD");

        Assert.Equal("ABC", trimmed);
    }

    /// <summary><see langword="null"/> input returns <see langword="null"/>.</summary>
    [Fact]
    public void TrimLabel_Null_ReturnsNull()
    {
        var trimmed = Avatar.TrimLabel(null);

        Assert.Null(trimmed);
    }

    /// <summary>An empty string returns <see langword="null"/>, since there is no label to show.</summary>
    [Fact]
    public void TrimLabel_Empty_ReturnsNull()
    {
        var trimmed = Avatar.TrimLabel(string.Empty);

        Assert.Null(trimmed);
    }

    /// <summary>A whitespace-only string returns <see langword="null"/>, since there is no label to show.</summary>
    [Fact]
    public void TrimLabel_Whitespace_ReturnsNull()
    {
        var trimmed = Avatar.TrimLabel("   ");

        Assert.Null(trimmed);
    }

    /// <summary>
    /// A zero-width-joiner family emoji (man, woman, girl, boy - eleven UTF-16 code units across four
    /// codepoints) counts as one text element, not seven, so it passes through unchanged rather than
    /// being cut mid-sequence.
    /// </summary>
    [Fact]
    public void TrimLabel_ZwjFamilyEmoji_CountsAsOneTextElement()
    {
        const string family = "\U0001F468‍\U0001F469‍\U0001F467‍\U0001F466";

        var trimmed = Avatar.TrimLabel(family);

        Assert.Equal(family, trimmed);
    }

    /// <summary>A skin-tone-modified emoji counts as one text element, not two.</summary>
    [Fact]
    public void TrimLabel_SkinToneEmoji_CountsAsOneTextElement()
    {
        const string thumbsUpMediumSkinTone = "\U0001F44D\U0001F3FD";

        var trimmed = Avatar.TrimLabel(thumbsUpMediumSkinTone);

        Assert.Equal(thumbsUpMediumSkinTone, trimmed);
    }

    /// <summary>Three emoji text elements pass through unchanged.</summary>
    [Fact]
    public void TrimLabel_ThreeEmoji_PassesUnchanged()
    {
        const string threeEmoji = "\U0001F600\U0001F601\U0001F602";

        var trimmed = Avatar.TrimLabel(threeEmoji);

        Assert.Equal(threeEmoji, trimmed);
    }

    /// <summary>Four emoji text elements truncate to the first three.</summary>
    [Fact]
    public void TrimLabel_FourEmoji_TruncatesToThree()
    {
        const string fourEmoji = "\U0001F600\U0001F601\U0001F602\U0001F603";
        const string firstThree = "\U0001F600\U0001F601\U0001F602";

        var trimmed = Avatar.TrimLabel(fourEmoji);

        Assert.Equal(firstThree, trimmed);
    }

    /// <summary><see cref="Avatar.None"/> has every field unset, so <see cref="Avatar.IsDefault"/> is true.</summary>
    [Fact]
    public void IsDefault_None_IsTrue()
    {
        Assert.True(Avatar.None.IsDefault);
    }

    /// <summary>Setting any one field - here just <see cref="Avatar.Label"/> - makes <see cref="Avatar.IsDefault"/> false.</summary>
    [Fact]
    public void IsDefault_LabelSet_IsFalse()
    {
        Avatar avatar = new(Label: "AB", Image: null, Background: null);

        Assert.False(avatar.IsDefault);
    }
}
