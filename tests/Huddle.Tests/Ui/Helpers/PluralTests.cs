namespace Agency.Huddle.Tests.Ui.Helpers;

/// <summary>
/// Pins <see cref="Agency.Huddle.App.Components.Shared.Plural.Of"/>: the count is written in front of the singular word for exactly one and
/// the plural word for every other count, so a Member list reads "1 member" and "2 members" (UX-R24).
/// </summary>
public sealed class PluralTests
{
    /// <summary>Each count picks the word its number needs, and the count is written in front of it.</summary>
    /// <param name="count">The number being described.</param>
    /// <param name="one">The singular word.</param>
    /// <param name="many">The plural word.</param>
    /// <param name="expected">The text the helper must return.</param>
    [Theory]
    [InlineData(0, "member", "members", "0 members")]
    [InlineData(1, "member", "members", "1 member")]
    [InlineData(2, "member", "members", "2 members")]
    [InlineData(1, "Room", "Rooms", "1 Room")]
    [InlineData(12, "Room", "Rooms", "12 Rooms")]
    public void Of_CountPicksTheSingularOrPluralWord(int count, string one, string many, string expected)
    {
        Assert.Equal(expected, Agency.Huddle.App.Components.Shared.Plural.Of(count, one, many));
    }
}
