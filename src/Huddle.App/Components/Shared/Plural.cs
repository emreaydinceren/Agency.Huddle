using System.Globalization;

namespace Agency.Huddle.App.Components.Shared;

/// <summary>Chooses the singular or plural word for a count, so every label reads "1 member" and "2 members".</summary>
internal static class Plural
{
    /// <summary>The count followed by the word its number needs: the singular for exactly one, the plural otherwise.</summary>
    /// <param name="count">The number being described.</param>
    /// <param name="one">The singular word.</param>
    /// <param name="many">The plural word.</param>
    /// <returns>The count and the chosen word, separated by a space.</returns>
    internal static string Of(int count, string one, string many) =>
        string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? one : many)}");
}
