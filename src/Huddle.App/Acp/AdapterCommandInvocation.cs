using Agency.Huddle.App.Services;
using System.Diagnostics.CodeAnalysis;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Decides, from a Message's text alone, whether it is an Adapter command addressed to one Agent:
/// <c>@Nova /compact keep the decisions</c> (Commands spec, sections 6.4 and 8.1). A pure function,
/// so it is tested exhaustively; it consults neither the allowlist nor the catalog, which the runner
/// does.
/// </summary>
/// <remarks>
/// The Mention is resolved against the Agent's <em>own</em> handles, longest first, never by a
/// pattern: a Name may contain spaces, so <c>@Emily Lee /compact</c> and <c>@Emily</c> followed by
/// the word <c>Lee</c> are the same characters, and only the handle can say where the Mention ends.
/// </remarks>
internal static class AdapterCommandInvocation
{
    /// <summary>Tries to read <paramref name="text"/> as a command for the Agent known by <paramref name="ownHandles"/>.</summary>
    /// <param name="text">The Message text.</param>
    /// <param name="ownHandles">The Agent's Name and Alias. A blank handle never matches.</param>
    /// <param name="call">The command, when this returns <see langword="true"/>.</param>
    /// <returns>
    /// <see langword="true"/> when, after leading whitespace, the text begins with this Agent's
    /// Mention, then whitespace, then <c>/</c> and a name. Any other shape, including a second Mention
    /// first, text before the Mention, or no space before the slash, is an ordinary Message.
    /// </returns>
    internal static bool TryParse(string text, IEnumerable<string> ownHandles, [NotNullWhen(true)] out AdapterCommandCall? call)
    {
        call = null;
        ReadOnlySpan<char> rest = text.AsSpan().TrimStart();
        if (rest.IsEmpty || rest[0] != '@')
        {
            return false;
        }

        rest = rest[1..];
        if (!TryConsumeHandle(rest, ownHandles, out rest))
        {
            return false;
        }

        // At least one whitespace must separate the Mention from the slash: "@Nova/compact" is text.
        ReadOnlySpan<char> afterSpace = rest.TrimStart();
        if (afterSpace.Length == rest.Length || afterSpace.IsEmpty || afterSpace[0] != '/')
        {
            return false;
        }

        ReadOnlySpan<char> afterSlash = afterSpace[1..];
        int nameLength = 0;
        while (nameLength < afterSlash.Length && !char.IsWhiteSpace(afterSlash[nameLength]))
        {
            nameLength++;
        }

        if (nameLength == 0)
        {
            return false;
        }

        call = new AdapterCommandCall(afterSlash[..nameLength].ToString(), afterSlash[nameLength..].Trim().ToString());
        return true;
    }

    /// <summary>
    /// Consumes the longest of <paramref name="ownHandles"/> that <paramref name="afterAt"/> starts with,
    /// so "Emily Lee" is read before "Emily" for an Agent that answers to both. A handle ends at a
    /// character that cannot continue a Name, exactly as <see cref="MentionParser"/> decides.
    /// </summary>
    /// <param name="afterAt">The text after the leading <c>@</c>.</param>
    /// <param name="ownHandles">The Agent's Name and Alias; a blank one never matches.</param>
    /// <param name="rest">The text after the handle, when this returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when a handle matched at a word boundary.</returns>
    private static bool TryConsumeHandle(ReadOnlySpan<char> afterAt, IEnumerable<string> ownHandles, out ReadOnlySpan<char> rest)
    {
        string? best = null;
        foreach (string handle in ownHandles)
        {
            if (string.IsNullOrWhiteSpace(handle) || (best is not null && handle.Length <= best.Length))
            {
                continue;
            }

            if (afterAt.StartsWith(handle, StringComparison.OrdinalIgnoreCase)
                && (afterAt.Length == handle.Length || !MentionParser.IsNameCharacter(afterAt[handle.Length])))
            {
                best = handle;
            }
        }

        rest = best is null ? default : afterAt[best.Length..];
        return best is not null;
    }
}
