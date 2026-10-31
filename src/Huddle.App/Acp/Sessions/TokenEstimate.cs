using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>
/// The three-characters-per-token estimate that the context trim and the fresh-start note share
/// (LR-R8). Correction C5 fixes the measure: one Message costs
/// <c>ceil((SenderName.Length + 2 + Text.Length) / 3)</c> tokens.
/// </summary>
internal static class TokenEstimate
{
    /// <summary>
    /// Estimates the tokens for a count of characters, rounding up to whole tokens at three characters per token (LR-R8, C5).
    /// </summary>
    /// <param name="chars">The number of characters to estimate; must not be negative.</param>
    /// <returns>The character count divided by three, rounded up.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="chars"/> is negative.</exception>
    internal static int OfChars(int chars)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(chars);

        return (chars + 2) / 3;
    }

    /// <summary>
    /// Estimates the tokens one Message costs: its sender name, two separator characters and its text, measured by <see cref="OfChars"/> (LR-R8, C5).
    /// </summary>
    /// <param name="message">The Message to estimate.</param>
    /// <returns>The estimated token cost of the Message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is null.</exception>
    internal static int OfMessage(ChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return OfChars(message.SenderName.Length + 2 + message.Text.Length);
    }
}
