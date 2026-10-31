using Agency.Huddle.App.Avatars;

namespace Agency.Huddle.Tests.Avatars;

/// <summary>
/// Pins the derived members of <see cref="LibraryAvatar"/>: the accessible name is built from the raw
/// <c>skin_tone</c> text rather than the group, and the group is looked up from that same tone.
/// </summary>
public sealed class LibraryAvatarTests
{
    /// <summary>The accessible name reads the raw skin tone, gender and profession in that order.</summary>
    [Fact]
    public void AccessibleName_KnownTone_UsesRawToneGenderAndProfession()
    {
        LibraryAvatar avatar = new("woman-podcaster.png", "Podcaster", "fair", "woman", []);

        string actual = avatar.AccessibleName;

        Assert.Equal("fair woman Podcaster", actual);
    }

    /// <summary>An avatar whose tone is in no group still gets its raw tone in the accessible name.</summary>
    [Fact]
    public void AccessibleName_UngroupedTone_KeepsRawTone()
    {
        LibraryAvatar avatar = new("olive-man-pilot.png", "Pilot", "olive", "man", []);

        string actual = avatar.AccessibleName;

        Assert.Equal("olive man Pilot", actual);
    }

    /// <summary>A fair or light tone is in the Light group.</summary>
    [Theory]
    [InlineData("fair", "Light")]
    [InlineData("light", "Light")]
    public void Group_LightTone_IsLight(string skinTone, string expectedGroup)
    {
        LibraryAvatar avatar = new("a.png", "Podcaster", skinTone, "woman", []);

        SkinGroup? actual = avatar.Group;

        Assert.Equal(expectedGroup, actual?.ToString());
    }

    /// <summary>A tone outside the documented set has no group.</summary>
    [Fact]
    public void Group_UngroupedTone_IsNull()
    {
        LibraryAvatar avatar = new("a.png", "Pilot", "olive", "man", []);

        SkinGroup? actual = avatar.Group;

        Assert.Null(actual);
    }
}
