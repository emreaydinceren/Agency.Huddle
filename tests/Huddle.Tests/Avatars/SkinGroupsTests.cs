using Agency.Huddle.App.Avatars;

namespace Agency.Huddle.Tests.Avatars;

/// <summary>
/// Pins <see cref="SkinGroups.From(string?)"/>: each documented <c>skin_tone</c> maps to exactly one
/// group, matching is ordinal and ignores case, and anything unrecognised, missing or empty belongs to
/// no group at all.
/// </summary>
public sealed class SkinGroupsTests
{
    /// <summary>Each recognised <c>skin_tone</c> value maps to its documented group.</summary>
    [Theory]
    [InlineData("fair", "Light")]
    [InlineData("light", "Light")]
    [InlineData("tan", "Tan")]
    [InlineData("brown", "Tan")]
    [InlineData("dark", "Dark")]
    public void From_RecognisedTone_ReturnsItsGroup(string skinTone, string expectedGroup)
    {
        SkinGroup? actual = SkinGroups.From(skinTone);

        Assert.Equal(expectedGroup, actual?.ToString());
    }

    /// <summary>Recognised tones match regardless of letter case, so <c>FAIR</c> and <c>Brown</c> still map.</summary>
    [Theory]
    [InlineData("FAIR", "Light")]
    [InlineData("Brown", "Tan")]
    [InlineData("DARK", "Dark")]
    public void From_RecognisedToneInOtherCase_ReturnsItsGroup(string skinTone, string expectedGroup)
    {
        SkinGroup? actual = SkinGroups.From(skinTone);

        Assert.Equal(expectedGroup, actual?.ToString());
    }

    /// <summary>A tone outside the documented set belongs to no group, so the avatar appears only under All.</summary>
    [Theory]
    [InlineData("olive")]
    [InlineData("medium")]
    [InlineData("")]
    public void From_UnrecognisedOrEmptyTone_ReturnsNull(string skinTone)
    {
        SkinGroup? actual = SkinGroups.From(skinTone);

        Assert.Null(actual);
    }

    /// <summary>A missing <c>skin_tone</c> (null) belongs to no group rather than throwing.</summary>
    [Fact]
    public void From_NullTone_ReturnsNull()
    {
        SkinGroup? actual = SkinGroups.From(null);

        Assert.Null(actual);
    }
}
