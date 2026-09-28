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
    private static readonly MarkdownPipeline Pipeline = MarkdownRenderer.CreateBuilder().UsePreciseSourceLocation().Build();

    [GeneratedRegex(@"(!?)\[\[([^\[\]\r\n]+?)\]\]", RegexOptions.CultureInvariant)]
    private static partial Regex WikiLinkRegex();

    /// <summary>Parses every wikilink in <paramref name="markdown"/>, in document order.</summary>
    /// <param name="markdown">The raw Markdown text of a note.</param>
    public static IReadOnlyList<WikiLink> Parse(string markdown)
    {
        MarkdownDocument document = Markdig.Markdown.Parse(markdown, WikiLinkParser.Pipeline);
        List<(int Start, int EndExclusive)> excluded = WikiLinkParser.CollectExcludedRanges(document);

        List<WikiLink> links = [];
        foreach (Match match in WikiLinkParser.WikiLinkRegex().Matches(markdown))
        {
            int start = match.Index;
            int endExclusive = start + match.Length;
            if (WikiLinkParser.Overlaps(excluded, start, endExclusive))
            {
                continue;
            }

            if (WikiLinkParser.IsEscapedAt(markdown, start))
            {
                continue;
            }

            bool isEmbed = match.Groups[1].Length > 0;
            string inner = match.Groups[2].Value;
            if (!WikiLinkParser.TryParseInner(inner, out string target, out string? heading, out string? alias))
            {
                continue;
            }

            links.Add(new WikiLink(target, heading, alias, isEmbed, WikiLinkParser.LineOf(markdown, start), start, match.Length));
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

    /// <summary>The 1-based line of <paramref name="index"/> in <paramref name="text"/>, counting a lone <c>\r</c> as a line break in addition to <c>\n</c>.</summary>
    private static int LineOf(string text, int index)
    {
        int line = 1;
        for (int i = 0; i < index; i++)
        {
            char c = text[i];
            if (c == '\n')
            {
                line++;
            }
            else if (c == '\r' && (i + 1 >= text.Length || text[i + 1] != '\n'))
            {
                line++;
            }
        }

        return line;
    }

    /// <summary>
    /// Splits the captured inner text into target, heading and alias. The closing <c>]]</c> being
    /// escaped (an odd trailing backslash run) leaves the match unterminated, so it is rejected
    /// here rather than by the regex. An empty target with no heading is not a link.
    /// </summary>
    private static bool TryParseInner(string inner, out string target, out string? heading, out string? alias)
    {
        target = string.Empty;
        heading = null;
        alias = null;

        if (WikiLinkParser.IsEscapedAt(inner, inner.Length))
        {
            return false;
        }

        string beforeAlias = inner;
        int pipeIndex = inner.IndexOf('|', StringComparison.Ordinal);
        if (pipeIndex >= 0)
        {
            beforeAlias = inner[..pipeIndex];
            if (beforeAlias.EndsWith('\\'))
            {
                beforeAlias = beforeAlias[..^1];
            }

            alias = inner[(pipeIndex + 1)..].Trim();
        }

        int hashIndex = beforeAlias.IndexOf('#', StringComparison.Ordinal);
        if (hashIndex >= 0)
        {
            target = beforeAlias[..hashIndex].Trim();
            heading = beforeAlias[(hashIndex + 1)..].Trim();
        }
        else
        {
            target = beforeAlias.Trim();
        }

        if (target.Length == 0 && string.IsNullOrEmpty(heading))
        {
            return false;
        }

        return true;
    }
}
