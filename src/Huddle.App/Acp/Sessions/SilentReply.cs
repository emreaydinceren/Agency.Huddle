namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>
/// The one token an Agent ends its reply with when it has nothing to add (#362, #354). A reply that is only
/// the token, or that ends with it, loses the token before it is posted, and the token never streams to the
/// page. Anywhere else in a reply it is ordinary text. The Prompt text and the Stop hook read the token from
/// here, so the wording the model is given can never drift from what the Room recognises.
/// </summary>
internal static class SilentReply
{
    /// <summary>The exact text, compared case-sensitively.</summary>
    internal const string Token = "NO_REPLY";

    /// <summary>
    /// Removes the token from the end of a reply, with the whitespace around it, and leaves every other reply as written.
    /// A token counts unless the character before it is an underscore or an ASCII upper-case letter, which would make it the end of a longer
    /// name; a digit, lower-case letter, punctuation or whitespace before it does not. A full stop right after the token is part of it, and a
    /// run of tokens is removed whole: the Stop hook's reply is appended to the answer with no separator, so a token can follow another.
    /// </summary>
    /// <param name="text">The whole reply.</param>
    /// <returns>The reply without its trailing tokens, or <paramref name="text"/> itself when it does not end with one.</returns>
    internal static string Strip(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        int end = text.Length;
        bool stripped = false;
        while (TryFindTokenStart(text.AsSpan(0, end), out int start))
        {
            end = start;
            stripped = true;
        }

        return stripped ? text[..end].TrimEnd() : text;
    }

    /// <summary>Finds a trailing token, with its full stop and the whitespace around it, that counts as silence.</summary>
    /// <param name="text">The reply so far.</param>
    /// <param name="start">Where the token begins.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> ends with a token that counts.</returns>
    private static bool TryFindTokenStart(ReadOnlySpan<char> text, out int start)
    {
        ReadOnlySpan<char> trimmed = text.TrimEnd();
        if (trimmed.EndsWith('.') && trimmed[..^1].EndsWith(Token, StringComparison.Ordinal))
        {
            trimmed = trimmed[..^1];
        }

        start = trimmed.Length - Token.Length;
        if (start < 0 || !trimmed.EndsWith(Token, StringComparison.Ordinal))
        {
            return false;
        }

        if (start == 0)
        {
            return true;
        }

        char before = trimmed[start - 1];
        bool joinsAName = before == '_' || char.IsAsciiLetterUpper(before);
        return !joinsAName || trimmed[..start].EndsWith(Token, StringComparison.Ordinal);
    }

    /// <summary>
    /// How much of a reply that is still arriving may be sent to the page: everything except a tail that could still turn out to be the token.
    /// Only the tail is held back, never the whole reply, so an answer streams while a trailing token does not.
    /// </summary>
    /// <param name="text">The reply so far.</param>
    /// <returns>The number of leading characters that are safe to stream.</returns>
    internal static int SafeLength(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        string trimmed = text.TrimEnd();
        int held = trimmed.Length;
        for (int length = Math.Min(Token.Length - 1, held); length > 0; length--)
        {
            if (trimmed.EndsWith(Token[..length], StringComparison.Ordinal))
            {
                held -= length;
                break;
            }
        }

        bool heldMore = held < trimmed.Length;
        while (true)
        {
            string head = trimmed[..held].TrimEnd();
            if (head.EndsWith(Token + ".", StringComparison.Ordinal))
            {
                held = head.Length - Token.Length - 1;
            }
            else if (head.EndsWith(Token, StringComparison.Ordinal))
            {
                held = head.Length - Token.Length;
            }
            else
            {
                break;
            }

            heldMore = true;
        }

        return heldMore ? held : text.Length;
    }
}
