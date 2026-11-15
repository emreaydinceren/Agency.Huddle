using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Agency.Huddle.App.Search;

/// <summary>
/// Decides whether a text matches an Agent's Room query: a plain phrase compared ignoring case, or a regular
/// expression built with a match timeout so a pattern that backtracks without end fails instead of hanging.
/// </summary>
internal sealed class RoomMatcher
{
    /// <summary>The longest one regular-expression match may run.</summary>
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    private readonly string phrase;

    private readonly Regex? regex;

    private RoomMatcher(string phrase, Regex? regex)
    {
        this.phrase = phrase;
        this.regex = regex;
    }

    /// <summary>A matcher for a plain phrase, compared ordinally and ignoring case.</summary>
    /// <param name="phrase">The phrase.</param>
    public static RoomMatcher ForPhrase(string phrase)
    {
        ArgumentException.ThrowIfNullOrEmpty(phrase);
        return new RoomMatcher(phrase, null);
    }

    /// <summary>A matcher for a .NET regular expression, ignoring case, with a match timeout.</summary>
    /// <param name="pattern">The pattern, used as written.</param>
    /// <param name="matcher">The matcher, or null when the pattern is not valid.</param>
    /// <param name="error">The parser's complaint, or empty when the pattern is valid.</param>
    public static bool TryForRegex(string pattern, [NotNullWhen(true)] out RoomMatcher? matcher, out string error)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        try
        {
            matcher = new RoomMatcher(pattern, new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout));
            error = string.Empty;
            return true;
        }
        catch (ArgumentException ex)
        {
            matcher = null;
            error = ex.Message;
            return false;
        }
    }

    /// <summary>Whether the text matches.</summary>
    /// <param name="text">The text.</param>
    /// <exception cref="RegexMatchTimeoutException">A regular expression ran past its timeout.</exception>
    public bool IsMatch(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return this.regex is null ? text.Contains(this.phrase, StringComparison.OrdinalIgnoreCase) : this.regex.IsMatch(text);
    }

    /// <summary>A window of the text around its first match, with line breaks collapsed.</summary>
    /// <param name="text">The text.</param>
    /// <param name="width">The window width in characters, before the ellipses.</param>
    /// <returns>The snippet, or null when the text does not match.</returns>
    /// <exception cref="RegexMatchTimeoutException">A regular expression ran past its timeout.</exception>
    public string? Snippet(string text, int width)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (this.regex is null)
        {
            string? around = SearchSnippet.Around(text, this.phrase, width);
            if (around is not null)
            {
                return around;
            }
        }

        int index;
        int length;
        if (this.regex is null)
        {
            index = text.IndexOf(this.phrase, StringComparison.OrdinalIgnoreCase);
            length = this.phrase.Length;
        }
        else
        {
            Match match = this.regex.Match(text);
            index = match.Success ? match.Index : -1;
            length = match.Length;
        }

        return index < 0 ? null : Window(text.Replace('\n', ' ').Replace('\r', ' '), index, length, width);
    }

    private static string Window(string flat, int index, int length, int width)
    {
        if (flat.Length <= width)
        {
            return flat.Trim();
        }

        int lead = Math.Max(0, (width - length) / 2);
        int start = Math.Min(Math.Max(0, index - lead), flat.Length - width);
        string prefix = start > 0 ? "…" : string.Empty;
        string suffix = start + width < flat.Length ? "…" : string.Empty;
        return string.Concat(prefix, flat[start..(start + width)].Trim(), suffix);
    }
}
