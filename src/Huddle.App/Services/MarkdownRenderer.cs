using System.Text.RegularExpressions;
using Markdig;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Services;

/// <summary>
/// Renders chat Markdown to safe HTML, and - when a resolver is given - rewrites Task ids into
/// links (Spec §13.13.2). <c>partial</c> only to host the <see cref="TaskReferenceRegex"/> source
/// generator.
/// </summary>
public static partial class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = MarkdownRenderer.CreateBuilder().Build();

    /// <summary>
    /// Builds the pipeline shared by <see cref="MarkdownRenderer"/> and
    /// <see cref="Agency.Huddle.App.Library.WikiLinkParser"/>, so both parse Markdown the same way.
    /// </summary>
    internal static MarkdownPipelineBuilder CreateBuilder() => new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseEmphasisExtras()
        .UseAutoLinks()
        .UseTaskLists()
        .DisableHtml();

    /// <summary>Renders <paramref name="markdown"/> to HTML with no Task id linking. Unchanged behaviour.</summary>
    /// <param name="markdown">The Markdown text to render.</param>
    public static string ToHtml(string markdown) => MarkdownRenderer.ToHtml(markdown, null);

    /// <summary>
    /// Renders <paramref name="markdown"/> to HTML. When <paramref name="tasks"/> is not
    /// <see langword="null"/>, every stand-alone token shaped like a Task id that
    /// <paramref name="tasks"/> resolves becomes a <c>task-ref</c> link; anything it does not
    /// resolve - an unknown id, or one written outside chat, code or an existing link - stays plain
    /// text. Pass <see langword="null"/> when Tasks are disabled (Spec §13.13.2, correction D15.1#9).
    /// </summary>
    /// <param name="markdown">The Markdown text to render.</param>
    /// <param name="tasks">The resolver for candidate Task ids, or <see langword="null"/> to link none.</param>
    public static string ToHtml(string markdown, ITaskReferenceResolver? tasks)
    {
        using var writer = new StringWriter();
        var renderer = new Markdig.Renderers.HtmlRenderer(writer)
        {
            LinkRewriter = url => IsSafe(url) ? url : "#",
        };
        Pipeline.Setup(renderer);

        var document = Markdown.Parse(markdown, Pipeline);
        MarkdownRenderer.MarkStandaloneBoldParagraphsAsHeadings(document);
        if (tasks is not null)
        {
            MarkdownRenderer.LinkTaskReferences(document, tasks);
        }

        renderer.Render(document);

        return writer.ToString();
    }

    /// <summary>
    /// Marks a top-level paragraph shaped like an ad-hoc section header - a Persona's only way of
    /// writing one, since it writes prose rather than reaching for <c>#</c> Markdown headings - with
    /// the <c>message-heading</c> class, so app.css can style it apart from a bullet's own
    /// "<c>**Label:**</c> description" lead-in. Both a header and a lead-in render an identical
    /// <c>&lt;strong&gt;</c>, and CSS alone cannot tell "the whole paragraph" from "the first few
    /// words of it", so the distinction is made here, at the one point that can see the full
    /// paragraph structure. Scoped to <paramref name="document"/>'s direct children only - a
    /// paragraph nested inside a list item or blockquote is never one of these standalone headers.
    /// </summary>
    /// <param name="document">The parsed Markdown document, mutated in place.</param>
    private static void MarkStandaloneBoldParagraphsAsHeadings(MarkdownDocument document)
    {
        foreach (Block block in document)
        {
            if (block is ParagraphBlock { Inline: { } inline } paragraph && MarkdownRenderer.IsHeadingShaped(inline))
            {
                paragraph.GetAttributes().AddClass("message-heading");
            }
        }
    }

    /// <summary>
    /// Whether <paramref name="inline"/> is exactly a <c>**bold**</c> run and nothing else, or that
    /// same bold run - followed by whatever whitespace Markdig kept as the space typed between the
    /// two - and exactly one trailing <c>*italic*</c> aside, as in
    /// <c>**Work for you** *(these cost more)*</c>, and nothing else.
    /// </summary>
    /// <param name="inline">A paragraph's inline content.</param>
    private static bool IsHeadingShaped(ContainerInline inline)
    {
        if (inline.FirstChild is not EmphasisInline { DelimiterCount: 2 } bold)
        {
            return false;
        }

        if (ReferenceEquals(bold, inline.LastChild))
        {
            return true;
        }

        Inline? afterBold = bold.NextSibling;
        if (afterBold is LiteralInline gap && string.IsNullOrWhiteSpace(gap.Content.ToString()))
        {
            afterBold = afterBold.NextSibling;
        }

        return afterBold is EmphasisInline { DelimiterCount: 1 } italic && ReferenceEquals(italic, inline.LastChild);
    }

    /// <summary>
    /// Walks every <see cref="LiteralInline"/> in <paramref name="document"/> - materialised into a
    /// list first, since the walk mutates the tree it is enumerating - and rewrites each resolved
    /// Task id token into a <c>task-ref</c> <see cref="LinkInline"/> (Spec §13.13.2 step 1-5).
    /// </summary>
    /// <param name="document">The parsed Markdown document, mutated in place.</param>
    /// <param name="tasks">The resolver for candidate Task ids.</param>
    private static void LinkTaskReferences(MarkdownDocument document, ITaskReferenceResolver tasks)
    {
        List<LiteralInline> literals = document.Descendants<LiteralInline>().ToList();
        foreach (LiteralInline literal in literals)
        {
            if (literal.ContainsParentOfType<LinkInline>())
            {
                continue;
            }

            MarkdownRenderer.RewriteLiteral(literal, tasks);
        }
    }

    /// <summary>
    /// Splits <paramref name="literal"/> around every token that resolves through
    /// <paramref name="tasks"/>, inserting a <c>task-ref</c> link for each and leaving the
    /// surrounding text as plain literals. Does nothing when no token in <paramref name="literal"/>
    /// resolves.
    /// </summary>
    /// <param name="literal">The literal being scanned; removed and replaced when at least one token resolves.</param>
    /// <param name="tasks">The resolver for candidate Task ids.</param>
    private static void RewriteLiteral(LiteralInline literal, ITaskReferenceResolver tasks)
    {
        string text = literal.Content.ToString();
        MatchCollection matches = MarkdownRenderer.TaskReferenceRegex().Matches(text);
        if (matches.Count == 0)
        {
            return;
        }

        List<Inline> replacements = [];
        int consumed = 0;
        foreach (Match match in matches)
        {
            if (!TaskId.TryParse(match.Value, out TaskId id))
            {
                continue;
            }

            TaskReference? reference = tasks.Resolve(id);
            if (reference is null)
            {
                continue;
            }

            if (match.Index > consumed)
            {
                replacements.Add(new LiteralInline(text[consumed..match.Index]));
            }

            replacements.Add(MarkdownRenderer.CreateLink(reference, match.Value));
            consumed = match.Index + match.Length;
        }

        if (replacements.Count == 0)
        {
            return;
        }

        if (consumed < text.Length)
        {
            replacements.Add(new LiteralInline(text[consumed..]));
        }

        Inline previous = literal;
        foreach (Inline replacement in replacements)
        {
            previous.InsertAfter(replacement);
            previous = replacement;
        }

        literal.Remove();
    }

    /// <summary>Builds the <c>task-ref</c> link for a resolved Task reference (Spec §13.13.2 step 5).</summary>
    /// <param name="reference">The resolved Task.</param>
    /// <param name="token">The id token exactly as typed, used as the link's text.</param>
    private static LinkInline CreateLink(TaskReference reference, string token)
    {
        LinkInline link = new(string.Concat("/tasks/item/", reference.Id.ToString()), reference.Title);
        link.AppendChild(new LiteralInline(token));
        link.GetAttributes().AddClass("task-ref");
        if (reference.Closed)
        {
            link.GetAttributes().AddClass("task-ref-closed");
        }

        return link;
    }

    /// <summary>
    /// Whether <paramref name="url"/> may reach the rendered page unchanged: an <c>http:</c>,
    /// <c>https:</c> or <c>mailto:</c> link, or a bare Task reference - <c>/tasks/item/</c> followed
    /// by exactly a valid <see cref="TaskId"/>'s canonical text and nothing else (Spec §13.13.2
    /// warning). Anything else, including a path-traversal attempt dressed up as one, is rewritten
    /// to <c>#</c>.
    /// </summary>
    /// <param name="url">The candidate link target.</param>
    private static bool IsSafe(string? url)
    {
        if (url is null)
        {
            return false;
        }

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        const string TaskItemPrefix = "/tasks/item/";
        if (!url.StartsWith(TaskItemPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        string remainder = url[TaskItemPrefix.Length..];
        return TaskId.TryParse(remainder, out TaskId id) && string.Equals(remainder, id.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Matches a stand-alone candidate Task id token: an upper-case prefix (D-32 - stricter than
    /// <see cref="TaskId.TryParse"/>, which also accepts lower case), a dash, and its number, with
    /// no letter, digit, underscore or dash immediately before or after.
    /// </summary>
    [GeneratedRegex(@"(?<![A-Za-z0-9_-])([A-Z][A-Z0-9]{0,7}-[0-9]{1,9})(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant)]
    private static partial Regex TaskReferenceRegex();
}
