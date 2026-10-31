using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Agency.Huddle.App.Library;

/// <summary>
/// Finds the inline <c>#tag</c>s in a note's body (Spec §6.17 <i>Inline syntax</i>). Like <see cref="WikiLinkParser"/> it scans the raw
/// text rather than walking the Markdig tree, and takes its skip ranges (code, link destinations, wikilinks, comments, bare URLs)
/// from several sources, because Markdig cannot supply most of them.
/// </summary>
internal static partial class NoteTagParser
{
    [GeneratedRegex(@"[A-Za-z][A-Za-z0-9+.\-]*://[^\s<>]*", RegexOptions.CultureInvariant)]
    private static partial Regex SchemeUrlRegex();

    /// <summary>Every inline tag in <paramref name="body"/>, normalised, in source order, repeats kept.</summary>
    /// <param name="body">The note's text after its frontmatter block.</param>
    internal static IReadOnlyList<string> Parse(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (!NoteTagParser.HasPlausibleTag(body))
        {
            return [];
        }

        List<(int Start, int EndExclusive)> skips = NoteTagParser.CollectSkips(body);
        skips.Sort(static (a, b) => a.Start.CompareTo(b.Start));

        List<string> tags = [];
        int skipIndex = 0;
        int index = 0;
        while (index < body.Length)
        {
            while (skipIndex < skips.Count && skips[skipIndex].EndExclusive <= index)
            {
                skipIndex++;
            }

            if (skipIndex < skips.Count && skips[skipIndex].Start <= index)
            {
                index = skips[skipIndex].EndExclusive;
                continue;
            }

            index = body[index] == '#' ? NoteTagParser.ScanTag(body, index, tags) : index + 1;
        }

        return tags;
    }

    /// <summary>A cheap raw scan: is there any <c>#</c> that could start a tag? Most notes' only <c>#</c>s are headings (<c># Title</c>),
    /// and those must not cost a Markdig parse.</summary>
    private static bool HasPlausibleTag(string body)
    {
        int hash = body.IndexOf('#', StringComparison.Ordinal);
        while (hash >= 0)
        {
            if (hash + 1 < body.Length
                && Rune.TryGetRuneAt(body, hash + 1, out Rune next)
                && NoteTagText.IsTagRune(next)
                && NoteTagParser.CanStartTag(body, hash))
            {
                return true;
            }

            hash = body.IndexOf('#', hash + 1);
        }

        return false;
    }

    /// <summary>Reads the tag that may start at the <c>#</c> at <paramref name="hash"/>, adds it when valid, and returns where to continue.</summary>
    private static int ScanTag(string body, int hash, List<string> tags)
    {
        if (!CanStartTag(body, hash))
        {
            return hash + 1;
        }

        int end = hash + 1;
        while (end < body.Length && Rune.TryGetRuneAt(body, end, out Rune rune) && NoteTagText.IsTagRune(rune))
        {
            end += rune.Utf16SequenceLength;
        }

        if (end > hash + 1 && NoteTagText.TryNormalise(body[(hash + 1)..end], out string tag))
        {
            tags.Add(tag);
        }

        return end;
    }

    /// <summary>(V) A <c>#</c> starts a tag unless the character before it is a letter, a digit, <c>&amp;</c> or <c>#</c>, or it is escaped by a backslash.</summary>
    private static bool CanStartTag(string body, int hash)
    {
        if (hash == 0)
        {
            return true;
        }

        // The scalar before the '#', not the UTF-16 unit: a letter outside the BMP is two units.
        int previousIndex = char.IsLowSurrogate(body[hash - 1]) && hash >= 2 ? hash - 2 : hash - 1;
        _ = Rune.TryGetRuneAt(body, previousIndex, out Rune previous);
        if (Rune.IsLetterOrDigit(previous) || previous.Value is '&' or '#' || NoteTagParser.IsMark(previous))
        {
            return false;
        }

        int backslashes = 0;
        for (int i = hash - 1; i >= 0 && body[i] == '\\'; i--)
        {
            backslashes++;
        }

        return backslashes % 2 == 0;
    }

    /// <summary>(V) A combining mark ends a word like a letter does, so <c>é#a</c> (decomposed) is not a tag start.</summary>
    private static bool IsMark(Rune rune) =>
        Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;

    private static List<(int Start, int EndExclusive)> CollectSkips(string body)
    {
        MarkdownDocument document = WikiLinkParser.ParseDocument(body);
        List<(int Start, int EndExclusive)> code = WikiLinkParser.CollectExcludedRanges(document);
        List<(int Start, int EndExclusive)> skips = [.. code];
        skips.AddRange(WikiLinkParser.LinkRanges(body, code));
        NoteTagParser.AddLinkDestinations(document, skips);
        NoteTagParser.AddComments(body, "%%", "%%", code, skips);
        NoteTagParser.AddComments(body, "<!--", "-->", code, skips);
        foreach (Match match in NoteTagParser.SchemeUrlRegex().Matches(body))
        {
            skips.Add((match.Index, match.Index + match.Length));
        }

        return skips;
    }

    /// <summary>A link's destination and an autolink are skipped; a link's label text counts.</summary>
    private static void AddLinkDestinations(MarkdownDocument document, List<(int Start, int EndExclusive)> skips)
    {
        foreach (LinkInline link in document.Descendants<LinkInline>())
        {
            if (link.IsAutoLink)
            {
                skips.Add((link.Span.Start, link.Span.End + 1));
            }
            else if (link.UrlSpan is { } url && url.Start >= 0 && url.End >= url.Start)
            {
                skips.Add((url.Start, url.End + 1));
            }
        }

        foreach (LinkReferenceDefinition definition in document.Descendants<LinkReferenceDefinition>())
        {
            skips.Add((definition.Span.Start, definition.Span.End + 1));
        }
    }

    /// <summary>(V) Skips every closed <paramref name="open"/> … <paramref name="close"/> pair; an unclosed opener is not a comment.</summary>
    private static void AddComments(string body, string open, string close, List<(int Start, int EndExclusive)> code, List<(int Start, int EndExclusive)> skips)
    {
        int position = 0;
        while (position < body.Length)
        {
            int start = NoteTagParser.IndexOutsideCode(body, open, position, code);
            if (start < 0)
            {
                return;
            }

            int end = NoteTagParser.IndexOutsideCode(body, close, start + open.Length, code);
            if (end < 0)
            {
                return;
            }

            skips.Add((start, end + close.Length));
            position = end + close.Length;
        }
    }

    /// <summary>The first index of <paramref name="marker"/> at or after <paramref name="from"/> that is not inside a code range; a marker in code opens or closes nothing.</summary>
    private static int IndexOutsideCode(string body, string marker, int from, List<(int Start, int EndExclusive)> code)
    {
        int index = body.IndexOf(marker, from, StringComparison.Ordinal);
        while (index >= 0 && code.Exists(range => index >= range.Start && index < range.EndExclusive))
        {
            index = body.IndexOf(marker, index + 1, StringComparison.Ordinal);
        }

        return index;
    }
}
