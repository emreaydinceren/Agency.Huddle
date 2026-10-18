namespace Agency.Huddle.Tests.Acp.Sessions;

using Agency.Huddle.App.Acp.Sessions;

/// <summary>
/// Exercises <see cref="RoomLabels.Distinguish"/> in isolation: a pure function, so every case is a
/// direct call with no pipe, no runner, and no session (D16 P0-3, RS §2 U15, RS D-20).
/// </summary>
public sealed class RoomLabelsTests
{
    /// <summary>A Room whose name no other known Room shares is returned unchanged.</summary>
    [Fact]
    public void Distinguish_UniqueName_Unchanged()
    {
        var knownNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["room-nova"] = "Nova",
            ["room-friend"] = "Friend",
        };

        var label = RoomLabels.Distinguish("room-nova", "Nova", knownNames);

        Assert.Equal("Nova", label);
    }

    /// <summary>
    /// Two Rooms named "Nova" get the losing one — the one being labelled — suffixed with " #" and
    /// the last six characters of its own id, so a model can tell them apart.
    /// </summary>
    [Fact]
    public void Distinguish_SharedName_AppendsHashAndLastSixOfId()
    {
        var knownNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["01J8PORTO4f2a91"] = "Nova",
            ["01J8OTHER000000"] = "Nova",
        };

        var label = RoomLabels.Distinguish("01J8PORTO4f2a91", "Nova", knownNames);

        Assert.Equal("Nova #4f2a91", label);
    }

    /// <summary>A model reads "Nova" and "nova" as one name, so the clash check ignores case.</summary>
    [Fact]
    public void Distinguish_SharedNameDifferentCase_AlsoSuffixed()
    {
        var knownNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["01J8PORTO4f2a91"] = "Nova",
            ["01J8OTHER000000"] = "nova",
        };

        var label = RoomLabels.Distinguish("01J8PORTO4f2a91", "Nova", knownNames);

        Assert.Equal("Nova #4f2a91", label);
    }

    /// <summary>An id shorter than six characters is used whole, rather than an empty or partial suffix.</summary>
    [Fact]
    public void Distinguish_IdShorterThanSix_UsesWholeId()
    {
        var knownNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["a1"] = "Nova",
            ["b2"] = "Nova",
        };

        var label = RoomLabels.Distinguish("a1", "Nova", knownNames);

        Assert.Equal("Nova #a1", label);
    }

    /// <summary>The Room's own entry in <c>knownNames</c> is not itself a clash.</summary>
    [Fact]
    public void Distinguish_OnlyOtherRoomsCount()
    {
        var knownNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["01J8PORTO4f2a91"] = "Nova",
        };

        var label = RoomLabels.Distinguish("01J8PORTO4f2a91", "Nova", knownNames);

        Assert.Equal("Nova", label);
    }
}
