using System.Text.RegularExpressions;

namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>
/// The tokens Memory Tidy refuses to see appear or vanish (spec §6.10): dates and times, numbers, links and e-mail addresses. A text is
/// scanned once; a span a kind has taken is not offered to the next, so a date is one token and a link's digits are not numbers.
/// </summary>
internal static partial class MemoryTidyTokens
{
    private const string LinkTrim = ".,;:!?)]}'\"";

    /// <summary>The checked tokens of a text, each once, in order of first appearance.</summary>
    /// <param name="text">The text.</param>
    internal static IReadOnlyList<MemoryTidyToken> Of(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        bool[] claimed = new bool[text.Length];
        List<(int Start, MemoryTidyToken Token)> found = [];

        // List markers are claimed first and yield no token, so a re-numbered list neither adds nor loses one.
        foreach (Match marker in ListMarker().Matches(text))
        {
            Claim(claimed, marker.Groups["n"].Index, marker.Groups["n"].Length);
        }

        Scan(text, LinkRegex(), MemoryTidyTokenKind.Link, claimed, found);
        Scan(text, AddressRegex(), MemoryTidyTokenKind.Address, claimed, found);
        Scan(text, DateRegex(), MemoryTidyTokenKind.Date, claimed, found);
        Scan(text, NumberRegex(), MemoryTidyTokenKind.Number, claimed, found);

        found.Sort(static (left, right) => left.Start.CompareTo(right.Start));
        HashSet<string> seen = new(StringComparer.Ordinal);
        List<MemoryTidyToken> tokens = [];
        foreach ((int _, MemoryTidyToken token) in found)
        {
            if (seen.Add(token.Key))
            {
                tokens.Add(token);
            }
        }

        return tokens;
    }

    /// <summary>The tokens of <paramref name="text"/> that none of <paramref name="others"/> holds, in order of first appearance.</summary>
    /// <param name="text">The text whose tokens are checked.</param>
    /// <param name="others">The texts they may come from.</param>
    internal static IReadOnlyList<MemoryTidyToken> NotIn(string text, IEnumerable<string> others)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(others);

        HashSet<string> known = new(StringComparer.Ordinal);
        foreach (string other in others)
        {
            foreach (MemoryTidyToken token in Of(other))
            {
                _ = known.Add(token.Key);
            }
        }

        return [.. Of(text).Where(token => !known.Contains(token.Key))];
    }

    /// <summary>The tokens of <paramref name="text"/> that <paramref name="other"/> does not hold, in order of first appearance.</summary>
    /// <param name="text">The text whose tokens are checked.</param>
    /// <param name="other">The text they may come from.</param>
    internal static IReadOnlyList<MemoryTidyToken> NotIn(string text, string other) => NotIn(text, [other]);

    private static void Scan(string text, Regex regex, MemoryTidyTokenKind kind, bool[] claimed, List<(int Start, MemoryTidyToken Token)> found)
    {
        foreach (Match match in regex.Matches(text))
        {
            string value = kind == MemoryTidyTokenKind.Link ? match.Value.TrimEnd(LinkTrim.ToCharArray()) : match.Value;
            if (value.Length == 0 || IsClaimed(claimed, match.Index, value.Length))
            {
                continue;
            }

            Claim(claimed, match.Index, value.Length);
            found.Add((match.Index, new MemoryTidyToken(kind, value)));
        }
    }

    private static bool IsClaimed(bool[] claimed, int start, int length)
    {
        for (int i = start; i < start + length; i++)
        {
            if (claimed[i])
            {
                return true;
            }
        }

        return false;
    }

    private static void Claim(bool[] claimed, int start, int length) => Array.Fill(claimed, true, start, length);

    /// <summary>Digits at the start of a line, followed by <c>.</c> or <c>)</c> and a space.</summary>
    [GeneratedRegex(@"^[ \t]*(?<n>[0-9]+)[.)](?=[ \t])", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex ListMarker();

    /// <summary><c>http(s)://</c>, <c>www.</c> and <c>mailto:</c> up to white space; a UNC path; a bare <c>name.tld/path</c>.</summary>
    [GeneratedRegex(@"(?:https?://|www\.|mailto:)\S+|\\\\[^\\\s]+\\\S+|(?<![\w@.-])[A-Za-z0-9][A-Za-z0-9-]*(?:\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}/\S*", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex LinkRegex();

    /// <summary>An e-mail address.</summary>
    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex AddressRegex();

    /// <summary>A whole date-shaped run (year first, or day and month with or without a year) or a time.</summary>
    [GeneratedRegex(@"(?<![0-9])(?:[0-9]{4}[-/.][0-9]{1,2}[-/.][0-9]{1,2}|[0-9]{1,2}[/.][0-9]{1,2}[/.][0-9]{2,4}|[0-9]{1,2}/[0-9]{1,2}|[0-9]{1,2}:[0-9]{2}(?::[0-9]{2})?)(?![0-9])", RegexOptions.CultureInvariant)]
    private static partial Regex DateRegex();

    /// <summary>A whole number with its separators, and a sign when it does not follow a letter or a digit.</summary>
    [GeneratedRegex(@"(?:(?<![\p{L}\p{N}])[-+])?[0-9]+(?:[.,][0-9]+)*", RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();
}
