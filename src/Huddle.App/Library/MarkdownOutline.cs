using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Services;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System.Text;

namespace Agency.Huddle.App.Library;

/// <summary>
/// The one place that names a Markdown heading's id, so the outline panel and the renderer agree on
/// it (Spec <i>Outline</i>, plan Task 6.2).
/// </summary>
internal static class MarkdownOutline
{
    private const string IdPrefix = "doc-h-";
    private const string EmptySlug = "section";

    private static readonly MarkdownPipeline Pipeline = MarkdownRenderer.CreateBuilder().Build();

    /// <summary>
    /// Builds the id of a heading from its plain text: <c>doc-h-</c> plus a lowercase slug in which
    /// letters and digits are kept, runs of whitespace and <c>-</c> become one <c>-</c>, and every
    /// other character is dropped. An empty slug becomes <c>section</c>.
    /// </summary>
    /// <param name="text">The heading's plain text.</param>
    /// <returns>The id, without any repeat suffix.</returns>
    internal static string Slug(string text)
    {
        StringBuilder builder = new();
        foreach (char c in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
            else if ((char.IsWhiteSpace(c) || c == '-') && builder.Length > 0 && builder[builder.Length - 1] != '-')
            {
                builder.Append('-');
            }
        }

        string slug = builder.ToString().Trim('-');
        return string.Concat(MarkdownOutline.IdPrefix, slug.Length == 0 ? MarkdownOutline.EmptySlug : slug);
    }

    /// <summary>
    /// Pairs every top-level heading of <paramref name="document"/> with its id. A repeated slug gets
    /// <c>-2</c>, <c>-3</c>, ... for its later occurrences, in document order.
    /// </summary>
    /// <param name="document">A parsed Markdown document.</param>
    /// <returns>The headings in document order, each with its id.</returns>
    internal static IReadOnlyList<(HeadingBlock Block, string Id)> IdsFor(MarkdownDocument document)
    {
        Dictionary<string, int> occurrences = new(StringComparer.Ordinal);
        List<(HeadingBlock Block, string Id)> ids = [];
        foreach (Block block in document)
        {
            if (block is not HeadingBlock heading)
            {
                continue;
            }

            string baseId = MarkdownOutline.Slug(MarkdownOutline.PlainText(heading.Inline));
            int count = occurrences.GetValueOrDefault(baseId) + 1;
            occurrences[baseId] = count;
            ids.Add((heading, count == 1 ? baseId : string.Concat(baseId, "-", count.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        }

        return ids;
    }

    /// <summary>
    /// Lists the top-level headings of a Markdown text. Frontmatter is split off first, as the Library
    /// does, and its lines are counted in each heading's line.
    /// </summary>
    /// <param name="markdown">The raw Markdown text of a document.</param>
    /// <returns>The headings in document order.</returns>
    internal static IReadOnlyList<OutlineHeading> Headings(string markdown)
    {
        (_, string body) = PersonaFrontmatter.Parse(markdown);
        int bodyOffset = markdown.EndsWith(body, StringComparison.Ordinal) ? markdown.Length - body.Length : 0;
        int frontmatterLines = MarkdownOutline.CountNewlines(markdown.AsSpan(0, bodyOffset));

        MarkdownDocument document = Markdig.Markdown.Parse(body, MarkdownOutline.Pipeline);
        List<OutlineHeading> headings = [];
        foreach ((HeadingBlock block, string id) in MarkdownOutline.IdsFor(document))
        {
            int line = MarkdownOutline.CountNewlines(body.AsSpan(0, block.Span.Start)) + 1 + frontmatterLines;
            headings.Add(new OutlineHeading(block.Level, MarkdownOutline.PlainText(block.Inline), id, line));
        }

        return headings;
    }

    /// <summary>
    /// The plain text of an inline container: literal text and code spans, recursing through emphasis
    /// and links.
    /// </summary>
    /// <param name="container">The heading's inline content, or <c>null</c> for an empty heading.</param>
    /// <returns>The concatenated plain text.</returns>
    private static string PlainText(ContainerInline? container)
    {
        StringBuilder builder = new();
        MarkdownOutline.AppendPlainText(container, builder);
        return builder.ToString();
    }

    /// <summary>Appends the plain text of one inline node and of its children.</summary>
    /// <param name="inline">The node to read.</param>
    /// <param name="builder">The buffer the text is appended to.</param>
    private static void AppendPlainText(Inline? inline, StringBuilder builder)
    {
        switch (inline)
        {
            case LiteralInline literal:
                builder.Append(literal.Content.ToString());
                break;
            case CodeInline code:
                builder.Append(code.Content);
                break;
            case ContainerInline container:
                for (Inline? child = container.FirstChild; child is not null; child = child.NextSibling)
                {
                    MarkdownOutline.AppendPlainText(child, builder);
                }

                break;
        }
    }

    /// <summary>Counts the line feeds in a span.</summary>
    /// <param name="text">The span to count.</param>
    /// <returns>The number of <c>\n</c> characters.</returns>
    private static int CountNewlines(ReadOnlySpan<char> text)
    {
        int count = 0;
        foreach (char c in text)
        {
            if (c == '\n')
            {
                count++;
            }
        }

        return count;
    }
}
