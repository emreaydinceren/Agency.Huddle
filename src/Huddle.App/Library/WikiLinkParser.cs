using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.App.Library;

/// <summary>
/// Parses wikilinks (<c>[[target]]</c> and its variants, Spec §6.5 <i>Parsing</i>) out of raw
/// Markdown text. Code spans, fenced code blocks and indented code blocks are excluded using a
/// Markdig parse; the links themselves are found with a regex over the raw text so that a
/// reference-link definition sharing the same brackets never hides a wikilink, and every position
/// is a raw-text offset rather than one of Markdig's own line numbers.
/// </summary>
internal static partial class WikiLinkParser
{
    private static readonly MarkdownPipeline Pipeline = MarkdownRenderer.CreateBuilder().Build();

    [GeneratedRegex(@"(!?)\[\[([^\[\]\r\n]+?)\]\]", RegexOptions.CultureInvariant)]
    private static partial Regex WikiLinkRegex();

    /// <summary>Parses every wikilink in <paramref name="markdown"/>, in document order.</summary>
    /// <param name="markdown">The raw Markdown text of a note.</param>
    public static IReadOnlyList<WikiLink> Parse(string markdown)
    {
        MarkdownDocument document = Markdig.Markdown.Parse(markdown, WikiLinkParser.Pipeline);
        List<(int Start, int EndExclusive)> excluded = WikiLinkParser.CollectExcludedRanges(document);
        return WikiLinkParser.ParseMatches(markdown, excluded);
    }

    /// <summary>
    /// Parses wikilinks in a raw slice of Markdown source that is already known to contain no code spans - e.g.
    /// the concatenated raw text of a run of sibling <see cref="Markdig.Syntax.Inlines.LiteralInline"/>s - so no
    /// document-level parse or exclusion pass runs. Positions in the returned <see cref="WikiLink"/>s are
    /// relative to <paramref name="slice"/>, not any larger document (<see cref="Agency.Huddle.App.Services.MarkdownRenderer"/>,
    /// Task 9.2.i, corrections-B5 item 19).
    /// </summary>
    /// <param name="slice">The raw Markdown text slice to scan.</param>
    internal static IReadOnlyList<WikiLink> ParseSlice(string slice)
    {
        ArgumentNullException.ThrowIfNull(slice);
        return WikiLinkParser.ParseMatches(slice, []);
    }

    /// <summary>The regex-matching loop shared by <see cref="Parse"/> and <see cref="ParseSlice"/>.</summary>
    /// <param name="text">The raw Markdown text to scan.</param>
    /// <param name="excluded">Source ranges (end-exclusive) to skip - code spans and code blocks; empty for a slice already known to hold none.</param>
    private static List<WikiLink> ParseMatches(string text, List<(int Start, int EndExclusive)> excluded)
    {
        List<WikiLink> links = [];
        int cursorIndex = 0;
        int cursorLine = 1;
        foreach (Match match in WikiLinkParser.WikiLinkRegex().Matches(text))
        {
            int start = match.Index;
            int endExclusive = start + match.Length;

            // Matches.Matches() yields matches in ascending Index order, so the line count between
            // the previous match and this one is never re-walked from the start of the text
            // (avoiding the quadratic cost of calling LineOf(markdown, start) per link).
            cursorLine += WikiLinkParser.CountLineBreaks(text, cursorIndex, start);
            cursorIndex = start;

            if (WikiLinkParser.Overlaps(excluded, start, endExclusive))
            {
                continue;
            }

            if (WikiLinkParser.IsEscapedAt(text, start))
            {
                continue;
            }

            bool isEmbed = match.Groups[1].Length > 0;
            string inner = match.Groups[2].Value;
            if (!WikiLinkParser.TryParseInner(inner, out string target, out string? heading, out string? alias))
            {
                continue;
            }

            links.Add(new WikiLink(target, heading, alias, isEmbed, cursorLine, start, match.Length));
        }

        return links;
    }

    /// <summary>The excluded source ranges (end-exclusive): every <see cref="CodeInline"/>, <see cref="FencedCodeBlock"/> and indented <see cref="CodeBlock"/>.</summary>
    private static List<(int Start, int EndExclusive)> CollectExcludedRanges(MarkdownDocument document)
    {
        List<(int, int)> ranges = [];
        foreach (CodeBlock codeBlock in document.Descendants<CodeBlock>())
        {
            ranges.Add((codeBlock.Span.Start, codeBlock.Span.End + 1));
        }

        foreach (CodeInline codeInline in document.Descendants<CodeInline>())
        {
            ranges.Add((codeInline.Span.Start, codeInline.Span.End + 1));
        }

        return ranges;
    }

    /// <summary>True when <c>[start, endExclusive)</c> overlaps any excluded range.</summary>
    private static bool Overlaps(List<(int Start, int EndExclusive)> ranges, int start, int endExclusive)
    {
        foreach ((int rangeStart, int rangeEndExclusive) in ranges)
        {
            if (start < rangeEndExclusive && endExclusive > rangeStart)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when the character (or run of characters) ending at <paramref name="index"/> in
    /// <paramref name="text"/> is escaped: an odd number of consecutive backslashes immediately
    /// precede it. Used for both the opening <c>[[</c> (against the raw text) and the closing
    /// <c>]]</c> (against the captured inner text) - the one place this rule lives.
    /// </summary>
    private static bool IsEscapedAt(string text, int index)
    {
        int backslashCount = 0;
        int i = index - 1;
        while (i >= 0 && text[i] == '\\')
        {
            backslashCount++;
            i--;
        }

        return backslashCount % 2 == 1;
    }

    /// <summary>The number of line breaks in <c>text[fromIndexInclusive..toIndexExclusive]</c>, counting a lone
    /// <c>\r</c> as a line break in addition to <c>\n</c> (the same rule <see cref="Parse"/> used to apply from
    /// index 0 on every call; called here over one bounded slice per match instead).</summary>
    private static int CountLineBreaks(string text, int fromIndexInclusive, int toIndexExclusive)
    {
        int count = 0;
        for (int i = fromIndexInclusive; i < toIndexExclusive; i++)
        {
            char c = text[i];
            bool lineFeed = c == '\n';
            bool loneCarriageReturn = c == '\r' && (i + 1 >= text.Length || text[i + 1] != '\n');
            if (lineFeed || loneCarriageReturn)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Splits the captured inner text into target, heading and alias. The closing <c>]]</c> being
    /// escaped (an odd trailing backslash run) leaves the match unterminated, so it is rejected
    /// here rather than by the regex. An empty target with no heading is not a link.
    /// </summary>
    private static bool TryParseInner(string inner, out string target, out string? heading, out string? alias)
        => WikiLinkParser.TryParseInner(inner, out target, out heading, out alias, out _, out _);

    /// <summary>
    /// The same grammar as the other <see cref="TryParseInner(string, out string, out string?, out string?)"/>
    /// overload, additionally reporting the offset and length of the trimmed target text within
    /// <paramref name="inner"/>. The one place this grammar is implemented; <see cref="TryGetTargetSpan"/>
    /// reuses it rather than re-deriving the rules.
    /// </summary>
    private static bool TryParseInner(string inner, out string target, out string? heading, out string? alias, out int targetStart, out int targetLength)
    {
        target = string.Empty;
        heading = null;
        alias = null;
        targetStart = 0;
        targetLength = 0;

        if (WikiLinkParser.IsEscapedAt(inner, inner.Length))
        {
            return false;
        }

        int beforeAliasLength = inner.Length;
        int pipeIndex = inner.IndexOf('|', StringComparison.Ordinal);
        if (pipeIndex >= 0)
        {
            beforeAliasLength = pipeIndex;
            if (pipeIndex > 0 && inner[pipeIndex - 1] == '\\')
            {
                beforeAliasLength--;
            }

            alias = inner[(pipeIndex + 1)..].Trim();
        }

        string beforeAlias = inner[..beforeAliasLength];

        int hashIndex = beforeAlias.IndexOf('#', StringComparison.Ordinal);
        string rawTarget = hashIndex >= 0 ? beforeAlias[..hashIndex] : beforeAlias;
        if (hashIndex >= 0)
        {
            heading = beforeAlias[(hashIndex + 1)..].Trim();
        }

        target = rawTarget.Trim();

        if (target.Length == 0 && string.IsNullOrEmpty(heading))
        {
            return false;
        }

        int leadingWhitespace = rawTarget.Length - rawTarget.TrimStart().Length;
        targetStart = leadingWhitespace;
        targetLength = target.Length;

        return true;
    }

    /// <summary>
    /// The offset and length, relative to <paramref name="markdown"/>, of the writable target
    /// text inside <paramref name="link"/>'s token (Spec §6.5 step 2: only the target changes,
    /// alias/heading/<c>!</c> are kept). Used by the rename/move link rewrite to replace only
    /// the target. Returns <see langword="false"/> when the token at <c>link.Start</c>/<c>link.Length</c>
    /// no longer matches what <paramref name="link"/> records - a stale position from before an
    /// earlier edit or another writer's change.
    /// </summary>
    internal static bool TryGetTargetSpan(string markdown, WikiLink link, out int start, out int length)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentNullException.ThrowIfNull(link);

        start = 0;
        length = 0;

        if (link.Start < 0 || link.Length < 0 || link.Start + link.Length > markdown.Length)
        {
            return false;
        }

        string token = markdown.Substring(link.Start, link.Length);
        int prefixLength = link.IsEmbed ? 3 : 2;
        int suffixLength = 2;
        if (token.Length < prefixLength + suffixLength)
        {
            return false;
        }

        string expectedPrefix = link.IsEmbed ? "![[" : "[[";
        if (!token.StartsWith(expectedPrefix, StringComparison.Ordinal) || !token.EndsWith("]]", StringComparison.Ordinal))
        {
            return false;
        }

        string inner = token[prefixLength..^suffixLength];
        if (!WikiLinkParser.TryParseInner(inner, out string target, out string? heading, out string? alias, out int targetStart, out int targetLength))
        {
            return false;
        }

        if (!string.Equals(target, link.Target, StringComparison.Ordinal)
            || !string.Equals(heading, link.Heading, StringComparison.Ordinal)
            || !string.Equals(alias, link.Alias, StringComparison.Ordinal))
        {
            return false;
        }

        start = link.Start + prefixLength + targetStart;
        length = targetLength;
        return true;
    }
}
