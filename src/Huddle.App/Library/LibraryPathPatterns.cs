using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Agency.Huddle.App.Library;

/// <summary>
/// Recognises absolute Windows drive paths, <c>file:///</c> URLs and UNC paths in chat text (Spec §6.6),
/// shared by <see cref="Agency.Huddle.App.Services.MarkdownRenderer"/> and Task D10's document collection.
/// </summary>
internal static partial class LibraryPathPatterns
{
    /// <summary>Trailing characters trimmed from a scanned match: they read as sentence punctuation, not path text.</summary>
    private static readonly char[] TrailingTrimChars = ['.', ',', ';', ':', ')', ']', '`'];

    /// <summary>Matches a space-free Windows drive path (<c>E:\Data\plan.md</c>) for scanning plain literal-run text.</summary>
    [GeneratedRegex(@"[A-Za-z]:\\[^\s<>""|?*]+", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsPathRegex();

    /// <summary>Matches a space-free <c>file:///</c> URL for scanning plain literal-run text.</summary>
    [GeneratedRegex(@"file:///\S+", RegexOptions.CultureInvariant)]
    private static partial Regex FileUrlRegex();

    /// <summary>Matches a space-free UNC path (<c>\\server\share\plan.md</c>) for scanning plain literal-run text.</summary>
    [GeneratedRegex(@"\\\\[^\s\\]+\\[^\s<>""|?*]+", RegexOptions.CultureInvariant)]
    private static partial Regex UncPathRegex();

    /// <summary>Matches a whole Windows drive path, spaces allowed, for a whole-string check on a code span's content.</summary>
    [GeneratedRegex(@"^[A-Za-z]:\\[^\r\n<>""|?*]+$", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsWholePathRegex();

    /// <summary>Matches a whole <c>file:///</c> URL for a whole-string check on a code span's content.</summary>
    [GeneratedRegex(@"^file:///\S+$", RegexOptions.CultureInvariant)]
    private static partial Regex FileUrlWholeRegex();

    /// <summary>Matches a whole UNC path, spaces allowed, for a whole-string check on a code span's content.</summary>
    [GeneratedRegex(@"^\\\\[^\r\n\\]+\\[^\r\n<>""|?*]+$", RegexOptions.CultureInvariant)]
    private static partial Regex UncWholePathRegex();

    /// <summary>
    /// Finds every space-free path, <c>file:</c> URL or UNC path in <paramref name="text"/>, in ascending order,
    /// with trailing sentence punctuation (<c>.</c> <c>,</c> <c>;</c> <c>:</c> <c>)</c> <c>]</c> and a stray
    /// backtick) trimmed from each match (corrections-B5 items 18 and 33).
    /// </summary>
    /// <param name="text">The raw source slice to scan.</param>
    internal static IReadOnlyList<(int Index, int Length, string Path)> Find(string text)
    {
        List<(int Index, int Length, string Path)> results = [];
        IEnumerable<Match> matches = LibraryPathPatterns.WindowsPathRegex().Matches(text)
            .Concat(LibraryPathPatterns.FileUrlRegex().Matches(text))
            .Concat(LibraryPathPatterns.UncPathRegex().Matches(text))
            .OrderBy(match => match.Index);

        foreach (Match match in matches)
        {
            int length = match.Length;
            while (length > 0 && Array.IndexOf(LibraryPathPatterns.TrailingTrimChars, match.Value[length - 1]) >= 0)
            {
                length--;
            }

            if (length == 0)
            {
                continue;
            }

            results.Add((match.Index, length, text.Substring(match.Index, length)));
        }

        return results;
    }

    /// <summary>
    /// Whether <paramref name="text"/>, taken as a whole - spaces included - is exactly one Windows drive path,
    /// <c>file:///</c> URL or UNC path, for the whole-code-span rule (Spec §6.6, corrections-B5 item 18).
    /// </summary>
    /// <param name="text">The candidate text - typically a whole <see cref="Markdig.Syntax.Inlines.CodeInline"/>'s content.</param>
    /// <param name="path">Set to <paramref name="text"/> when it matches; otherwise <see langword="null"/>.</param>
    internal static bool TryMatchWhole(string text, [NotNullWhen(true)] out string? path)
    {
        if (text.Length > 0 &&
            (LibraryPathPatterns.WindowsWholePathRegex().IsMatch(text) ||
             LibraryPathPatterns.FileUrlWholeRegex().IsMatch(text) ||
             LibraryPathPatterns.UncWholePathRegex().IsMatch(text)))
        {
            path = text;
            return true;
        }

        path = null;
        return false;
    }
}
