using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Helpers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Agency.Huddle.App.Library;
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
        .UsePreciseSourceLocation()
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
    public static string ToHtml(string markdown, ITaskReferenceResolver? tasks) =>
        MarkdownRenderer.ToHtml(markdown, tasks, library: null);

    /// <summary>
    /// Renders <paramref name="markdown"/> to HTML as the two-argument overload does, additionally linking
    /// absolute file paths and <c>file:</c> URLs written in chat text into <c>library-ref</c> links (Spec §6.6)
    /// when <paramref name="library"/> resolves them. With <paramref name="library"/> <see langword="null"/> -
    /// the default while the Library is disabled - behaviour is identical to
    /// <see cref="ToHtml(string, ITaskReferenceResolver?)"/>.
    /// </summary>
    /// <param name="markdown">The Markdown text to render.</param>
    /// <param name="tasks">The resolver for candidate Task ids, or <see langword="null"/> to link none.</param>
    /// <param name="library">The resolver for chat-text paths, or <see langword="null"/> to link none.</param>
    public static string ToHtml(string markdown, ITaskReferenceResolver? tasks, ILibraryReferenceResolver? library) =>
        MarkdownRenderer.Render(markdown, tasks, library, noteLibrary: null, from: null);

    /// <summary>
    /// Renders <paramref name="markdown"/> to HTML as the two-argument overload does, additionally linking
    /// wikilinks and relative Markdown links written in a Library note (Spec §6.6) when both
    /// <paramref name="library"/> and <paramref name="from"/> are given. With either <see langword="null"/> -
    /// the chat surface's case - behaviour is identical to <see cref="ToHtml(string, ITaskReferenceResolver?)"/>.
    /// </summary>
    /// <param name="markdown">The Markdown text to render.</param>
    /// <param name="tasks">The resolver for candidate Task ids, or <see langword="null"/> to link none.</param>
    /// <param name="library">The resolver for wikilinks and relative links, or <see langword="null"/> to link none.</param>
    /// <param name="from">The Library note <paramref name="markdown"/> was read from, or <see langword="null"/> outside a note.</param>
    internal static string ToHtml(string markdown, ITaskReferenceResolver? tasks, ILibraryNoteResolver? library, LibraryPath? from) =>
        MarkdownRenderer.Render(markdown, tasks, library: null, noteLibrary: library, from: from);

    /// <summary>The single render path shared by every public and internal <see cref="ToHtml(string)"/> overload.</summary>
    /// <param name="markdown">The Markdown text to render.</param>
    /// <param name="tasks">The resolver for candidate Task ids, or <see langword="null"/> to link none.</param>
    /// <param name="library">The resolver for chat-text paths, or <see langword="null"/> to link none.</param>
    /// <param name="noteLibrary">The resolver for wikilinks and relative links, or <see langword="null"/> outside a note.</param>
    /// <param name="from">The Library note <paramref name="markdown"/> was read from, or <see langword="null"/> outside a note.</param>
    private static string Render(string markdown, ITaskReferenceResolver? tasks, ILibraryReferenceResolver? library, ILibraryNoteResolver? noteLibrary, LibraryPath? from)
    {
        // An unguessable per-render marker, not something a note's own authored text could ever contain: only
        // an image href this call itself resolved through RewriteRelativeLinks carries it, so IsSafe can trust
        // the /library-files/ route for exactly that href and refuse the identical text written as a plain
        // "a href" (corrections-B6 item 30).
        string trustToken = Guid.NewGuid().ToString("N");
        using var writer = new StringWriter();
        var renderer = new Markdig.Renderers.HtmlRenderer(writer)
        {
            LinkRewriter = url => MarkdownRenderer.RewriteUrl(url, trustToken),
        };
        Pipeline.Setup(renderer);

        var document = Markdown.Parse(markdown, Pipeline);
        MarkdownRenderer.MarkStandaloneBoldParagraphsAsHeadings(document);
        if (tasks is not null)
        {
            MarkdownRenderer.LinkTaskReferences(document, tasks);
        }

        if (noteLibrary is not null && from is not null)
        {
            MarkdownRenderer.RewriteRelativeLinks(document, noteLibrary, from, trustToken);
            MarkdownRenderer.LinkWikiLinks(document, noteLibrary, from, markdown);
        }

        if (library is not null)
        {
            MarkdownRenderer.LinkLibraryPaths(document, library, markdown);
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
                replacements.Add(MarkdownRenderer.CreatePositionedLiteral(literal, text, consumed, match.Index));
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
            replacements.Add(MarkdownRenderer.CreatePositionedLiteral(literal, text, consumed, text.Length));
        }

        Inline previous = literal;
        foreach (Inline replacement in replacements)
        {
            previous.InsertAfter(replacement);
            previous = replacement;
        }

        literal.Remove();
    }

    /// <summary>
    /// Builds a replacement <see cref="LiteralInline"/> for <paramref name="original"/>'s <c>[start, end)</c>
    /// content slice, carrying over a proportional <c>Span</c> when <paramref name="original"/>'s own
    /// <c>Span</c> is set and covers exactly its <see cref="LiteralInline.Content"/> (no escape shrank the raw
    /// source relative to the unescaped text) - so a literal-run scan run later (wikilinks, Library paths) still
    /// sees a correct raw-source position for text <see cref="RewriteLiteral"/> split around a Task reference.
    /// When the proportional mapping cannot be trusted, the new literal is left with no <c>Span</c>, same as
    /// before this method existed.
    /// </summary>
    /// <param name="original">The literal being split.</param>
    /// <param name="text">The literal's own <see cref="LiteralInline.Content"/>, as a string.</param>
    /// <param name="start">The slice's start offset into <paramref name="text"/>, inclusive.</param>
    /// <param name="end">The slice's end offset into <paramref name="text"/>, exclusive.</param>
    private static LiteralInline CreatePositionedLiteral(LiteralInline original, string text, int start, int end)
    {
        LiteralInline result = new(text[start..end]);
        if (original.Span.End >= original.Span.Start && original.Span.Length == text.Length)
        {
            result.Span = new SourceSpan(original.Span.Start + start, original.Span.Start + end - 1);
        }

        return result;
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
    /// Rewrites a relative Markdown link (<c>[x](../plan.md)</c>) written in a Library note into a
    /// <c>?library=</c> href, in place, when <see cref="ILibraryNoteResolver.ResolveRelative"/> resolves it
    /// (Spec §6.6). An image link (<see cref="LinkInline.IsImage"/>) is instead rewritten into a
    /// <c>/library-files/</c> href (Task 12.8's endpoint; corrections-B6 item 30, superseding B5 item 21's
    /// "stays #"), prefixed with <paramref name="trustToken"/> so <see cref="RewriteUrl"/> can recognise it as
    /// generated for an image by this render call and strip the marker back off - the identical URL text
    /// authored as a plain <c>a href</c> never carries the marker and still renders <c>#</c>. A link the
    /// resolver does not recognise as a relative file link (an absolute URL, <c>mailto:</c>, a bare heading
    /// anchor) is left as today.
    /// </summary>
    /// <param name="document">The parsed Markdown document, mutated in place.</param>
    /// <param name="library">The resolver for relative links.</param>
    /// <param name="from">The Library note the links were written in.</param>
    /// <param name="trustToken">This render call's marker; see <see cref="RewriteUrl"/>.</param>
    private static void RewriteRelativeLinks(MarkdownDocument document, ILibraryNoteResolver library, LibraryPath from, string trustToken)
    {
        foreach (LinkInline link in document.Descendants<LinkInline>().ToList())
        {
            if (link.Url is null)
            {
                continue;
            }

            LibraryReference? reference = library.ResolveRelative(from, link.Url);
            if (reference is null)
            {
                continue;
            }

            if (link.IsImage)
            {
                link.Url = string.Concat(trustToken, MarkdownRenderer.BuildLibraryFilesHref(reference.RootId, reference.RelativePath));
                continue;
            }

            link.Url = MarkdownRenderer.BuildLibraryHref(reference.RootId, reference.RelativePath);
            link.GetAttributes().AddClass("library-ref");
            if (!reference.Exists)
            {
                link.GetAttributes().AddClass("library-ref-missing");
            }
        }
    }

    /// <summary>
    /// Finds every run of consecutive sibling <see cref="LiteralInline"/>s in <paramref name="document"/> and
    /// links each wikilink found in its RAW source slice (corrections-B5 item 19: Markdig splits <c>[[x]]</c> at
    /// <c>[</c>, so a single wikilink token can straddle several sibling literals; scanning each literal's own
    /// text separately would miss it, and reconstructing text from the raw slice for the KEPT portions would
    /// undo escapes such as <c>\*</c>). The containers are materialised into a list first, since linking mutates
    /// the tree being walked.
    /// </summary>
    /// <param name="document">The parsed Markdown document, mutated in place.</param>
    /// <param name="library">The resolver for wikilinks.</param>
    /// <param name="from">The Library note the wikilinks were written in.</param>
    /// <param name="markdown">The original raw Markdown text <paramref name="document"/> was parsed from - each inline's <c>Span</c> positions are offsets into this exact string.</param>
    private static void LinkWikiLinks(MarkdownDocument document, ILibraryNoteResolver library, LibraryPath from, string markdown) =>
        MarkdownRenderer.ForEachLiteralRun(document, run => MarkdownRenderer.LinkWikiLinksInRun(run, library, from, markdown));

    /// <summary>
    /// Finds every run of consecutive sibling <see cref="LiteralInline"/>s reachable from <paramref name="document"/>
    /// - stopping at any other inline or a run already inside a <see cref="LinkInline"/>, which breaks or excludes
    /// the run - and invokes <paramref name="handler"/> once per run (possibly empty). Shared by
    /// <see cref="LinkWikiLinks"/> and <see cref="LinkLibraryPaths"/>, since both scan the raw source slice of a
    /// literal run rather than each literal's own text (corrections-B5 item 19).
    /// </summary>
    /// <param name="document">The parsed Markdown document.</param>
    /// <param name="handler">Invoked once per run of consecutive sibling literals within one container.</param>
    private static void ForEachLiteralRun(MarkdownDocument document, Action<List<LiteralInline>> handler)
    {
        List<ContainerInline> containers = document.Descendants<LiteralInline>()
            .Select(literal => literal.Parent)
            .OfType<ContainerInline>()
            .Distinct()
            .ToList();

        foreach (ContainerInline container in containers)
        {
            if (container is LinkInline || container.ContainsParentOfType<LinkInline>())
            {
                // Text already inside a link (a Task reference just linked, or an explicit Markdown link) never
                // gets a nested link.
                continue;
            }

            List<LiteralInline> run = [];
            for (Inline? child = container.FirstChild; child is not null; child = child.NextSibling)
            {
                if (child is LiteralInline literal)
                {
                    run.Add(literal);
                    continue;
                }

                handler(run);
                run = [];
            }

            handler(run);
        }
    }

    /// <summary>
    /// Scans one run's raw source slice (the first literal's <c>Span</c> start to the last's end)
    /// for wikilinks via <see cref="WikiLinkParser.ParseSlice"/>, and - only when at least one is found - splices
    /// a <see cref="LinkInline"/> in for each via <see cref="SpliceRunMatches"/>, keeping the original
    /// <see cref="LiteralInline"/> objects (or <see cref="StringSlice"/> sub-ranges of them, when a link boundary
    /// falls inside one) for every kept portion in between.
    /// </summary>
    /// <param name="run">Consecutive sibling literals to scan; left untouched when empty or no wikilink is found.</param>
    /// <param name="library">The resolver for wikilinks.</param>
    /// <param name="from">The Library note the wikilinks were written in.</param>
    /// <param name="markdown">The original raw Markdown text.</param>
    private static void LinkWikiLinksInRun(List<LiteralInline> run, ILibraryNoteResolver library, LibraryPath from, string markdown)
    {
        if (!MarkdownRenderer.HasUsableSpans(run, markdown))
        {
            return;
        }

        int runStart = run[0].Span.Start;
        int runEndExclusive = run[^1].Span.End + 1;
        IReadOnlyList<WikiLink> links = WikiLinkParser.ParseSlice(markdown.Substring(runStart, runEndExclusive - runStart));
        if (links.Count == 0)
        {
            return;
        }

        List<(int Start, int Length, Func<string, Inline> Build)> matches = [.. links.Select(link =>
            (runStart + link.Start, link.Length, (Func<string, Inline>)(rawToken => MarkdownRenderer.CreateWikiLinkInline(library, from, link, rawToken))))];
        MarkdownRenderer.SpliceRunMatches(run, matches, markdown);
    }

    /// <summary>
    /// Splices <paramref name="run"/> around each of <paramref name="matches"/> (given as raw-source offsets, in
    /// ascending order), replacing each match's span with the <see cref="Inline"/> its own builder produces and
    /// keeping every portion in between as the original <see cref="LiteralInline"/> content (or a
    /// <see cref="StringSlice"/> sub-range of it, via <see cref="AppendKeptRange"/>). Shared by
    /// <see cref="LinkWikiLinksInRun"/> and the Library path pass, so the raw-slice scanning and splicing logic
    /// exists exactly once.
    /// </summary>
    /// <param name="run">Consecutive sibling literals to splice; left untouched when empty.</param>
    /// <param name="matches">Non-overlapping matches in ascending <c>Start</c> order, each with its own replacement builder.</param>
    /// <param name="markdown">The original raw Markdown text.</param>
    private static void SpliceRunMatches(List<LiteralInline> run, List<(int Start, int Length, Func<string, Inline> Build)> matches, string markdown)
    {
        if (run.Count == 0 || matches.Count == 0)
        {
            return;
        }

        List<(LiteralInline Literal, int Start, int EndExclusive)> positions = [.. run.Select(literal => (literal, literal.Span.Start, literal.Span.End + 1))];
        int runStart = positions[0].Start;
        int runEndExclusive = positions[^1].EndExclusive;

        List<Inline> replacements = [];
        int cursor = runStart;
        int literalIndex = 0;
        foreach ((int start, int length, Func<string, Inline> build) in matches)
        {
            MarkdownRenderer.AppendKeptRange(replacements, positions, ref literalIndex, cursor, start);
            replacements.Add(build(markdown.Substring(start, length)));
            cursor = start + length;
        }

        MarkdownRenderer.AppendKeptRange(replacements, positions, ref literalIndex, cursor, runEndExclusive);

        Inline previous = run[0];
        foreach (Inline replacement in replacements)
        {
            previous.InsertAfter(replacement);
            previous = replacement;
        }

        foreach (LiteralInline literal in run)
        {
            literal.Remove();
        }
    }

    /// <summary>
    /// Finds absolute file paths, <c>file:</c> URLs and UNC paths in <paramref name="document"/> and links each
    /// one into a <c>library-ref</c> <see cref="LinkInline"/> when <paramref name="library"/> resolves it (Spec
    /// §6.6): a whole <see cref="CodeInline"/> whose content is exactly one such path (spaces allowed), and every
    /// space-free occurrence in plain literal-run text (correction B5 item 18, H5).
    /// </summary>
    /// <param name="document">The parsed Markdown document, mutated in place.</param>
    /// <param name="library">The resolver for chat-text paths.</param>
    /// <param name="markdown">The original raw Markdown text.</param>
    private static void LinkLibraryPaths(MarkdownDocument document, ILibraryReferenceResolver library, string markdown)
    {
        MarkdownRenderer.LinkCodeSpanLibraryPaths(document, library);
        MarkdownRenderer.ForEachLiteralRun(document, run => MarkdownRenderer.LinkLibraryPathsInRun(run, library, markdown));
    }

    /// <summary>
    /// Wraps a whole <see cref="CodeInline"/> in a <c>library-ref</c> <see cref="LinkInline"/> when its entire
    /// content, spaces included, is exactly one path <see cref="LibraryPathPatterns.TryMatchWhole"/> recognises
    /// and <paramref name="library"/> resolves. The code element itself is kept as the link's only child, so no
    /// backtick ever appears in the rendered text.
    /// </summary>
    /// <param name="document">The parsed Markdown document, mutated in place.</param>
    /// <param name="library">The resolver for chat-text paths.</param>
    private static void LinkCodeSpanLibraryPaths(MarkdownDocument document, ILibraryReferenceResolver library)
    {
        foreach (CodeInline code in document.Descendants<CodeInline>().ToList())
        {
            if (code.ContainsParentOfType<LinkInline>())
            {
                continue;
            }

            if (!LibraryPathPatterns.TryMatchWhole(code.Content, out string? path))
            {
                continue;
            }

            LibraryReference? reference = library.ResolvePath(path);
            if (reference is null)
            {
                continue;
            }

            LinkInline link = new(MarkdownRenderer.BuildLibraryHref(reference.RootId, reference.RelativePath), string.Empty);
            link.GetAttributes().AddClass("library-ref");
            if (!reference.Exists)
            {
                link.GetAttributes().AddClass("library-ref-missing");
            }

            code.InsertBefore(link);
            code.Remove();
            link.AppendChild(code);
        }
    }

    /// <summary>
    /// Whether <paramref name="run"/> can be mapped back to raw source: non-empty, and every literal's Span set,
    /// in order and inside <paramref name="markdown"/>. Every literal should have one under the precise-location
    /// pipeline (judgement 40), but a run that doesn't is skipped (left unlinked) rather than allowed to throw
    /// out of a chat render.
    /// </summary>
    private static bool HasUsableSpans(List<LiteralInline> run, string markdown)
    {
        if (run.Count == 0)
        {
            return false;
        }

        int previousEnd = -1;
        foreach (LiteralInline literal in run)
        {
            if (literal.Span.Start < 0 || literal.Span.End < literal.Span.Start || literal.Span.End >= markdown.Length || literal.Span.Start <= previousEnd)
            {
                return false;
            }

            previousEnd = literal.Span.End;
        }

        return true;
    }

    /// <summary>Scans one literal run's raw source slice for space-free paths via <see cref="LibraryPathPatterns.Find"/>
    /// and splices in a <c>library-ref</c> link for each one <paramref name="library"/> resolves, via <see cref="SpliceRunMatches"/>.</summary>
    /// <param name="run">Consecutive sibling literals to scan; left untouched when empty or no path is found.</param>
    /// <param name="library">The resolver for chat-text paths.</param>
    /// <param name="markdown">The original raw Markdown text.</param>
    private static void LinkLibraryPathsInRun(List<LiteralInline> run, ILibraryReferenceResolver library, string markdown)
    {
        if (!MarkdownRenderer.HasUsableSpans(run, markdown))
        {
            return;
        }

        int runStart = run[0].Span.Start;
        int runEndExclusive = run[^1].Span.End + 1;
        IReadOnlyList<(int Index, int Length, string Path)> found = LibraryPathPatterns.Find(markdown.Substring(runStart, runEndExclusive - runStart));
        if (found.Count == 0)
        {
            return;
        }

        List<(int Start, int Length, Func<string, Inline> Build)> matches = [.. found.Select(m =>
            (runStart + m.Index, m.Length, (Func<string, Inline>)(rawToken => MarkdownRenderer.CreateLibraryPathLink(library, rawToken))))];
        MarkdownRenderer.SpliceRunMatches(run, matches, markdown);
    }

    /// <summary>Resolves <paramref name="path"/> through <paramref name="library"/>: a <c>library-ref</c>
    /// <see cref="LinkInline"/> when it resolves (dimmed via <c>library-ref-missing</c> when the file no longer
    /// exists), or a plain literal holding <paramref name="path"/> exactly as written when it does not.</summary>
    /// <param name="library">The resolver for chat-text paths.</param>
    /// <param name="path">The matched path text, exactly as it appears in the source.</param>
    private static Inline CreateLibraryPathLink(ILibraryReferenceResolver library, string path)
    {
        LibraryReference? reference = library.ResolvePath(path);
        if (reference is null)
        {
            return new LiteralInline(path);
        }

        LinkInline link = new(MarkdownRenderer.BuildLibraryHref(reference.RootId, reference.RelativePath), string.Empty);
        link.AppendChild(new LiteralInline(path));
        link.GetAttributes().AddClass("library-ref");
        if (!reference.Exists)
        {
            link.GetAttributes().AddClass("library-ref-missing");
        }

        return link;
    }

    /// <summary>
    /// Appends the kept (non-wikilink) inlines covering <c>[fromInclusive, toExclusive)</c> in raw-source
    /// coordinates: a whole original literal's <see cref="LiteralInline.Content"/> (wrapped in a fresh node)
    /// when its computed position lies entirely inside the range, or a new <see cref="LiteralInline"/> built
    /// from a <see cref="StringSlice"/> sub-range of it when a boundary falls inside it. <paramref name="literalIndex"/>
    /// tracks how far into <paramref name="positions"/> the scan has advanced, since ranges are visited in
    /// ascending order across one run.
    /// </summary>
    private static void AppendKeptRange(List<Inline> replacements, List<(LiteralInline Literal, int Start, int EndExclusive)> positions, ref int literalIndex, int fromInclusive, int toExclusive)
    {
        while (fromInclusive < toExclusive && literalIndex < positions.Count)
        {
            (LiteralInline literal, int literalStart, int literalEndExclusive) = positions[literalIndex];

            if (literalEndExclusive <= fromInclusive)
            {
                literalIndex++;
                continue;
            }

            if (literalStart >= toExclusive)
            {
                break;
            }

            int overlapStart = Math.Max(literalStart, fromInclusive);
            int overlapEndExclusive = Math.Min(literalEndExclusive, toExclusive);

            if (overlapStart == literalStart && overlapEndExclusive == literalEndExclusive)
            {
                // A fresh node wrapping the SAME Content StringSlice (a value type: no aliasing risk) - the
                // original literal cannot be reused directly, since it is still attached to the tree at this
                // point and InsertAfter refuses a node that already has a parent.
                replacements.Add(new LiteralInline(literal.Content));
            }
            else
            {
                int contentStart = literal.Content.Start + (overlapStart - literalStart);
                int contentEnd = contentStart + (overlapEndExclusive - overlapStart) - 1;
                if (contentStart >= literal.Content.Start && contentEnd <= literal.Content.End)
                {
                    replacements.Add(new LiteralInline(new StringSlice(literal.Content.Text, contentStart, contentEnd)));
                }
                else
                {
                    // The literal's Content doesn't map 1:1 to its computed position (an escape literal, e.g.
                    // "\*" -> "*"): keep the whole original text rather than mis-slice it. Not reachable by a
                    // wikilink boundary, since "[[", "]]" and "![[" are never themselves an escaped character.
                    replacements.Add(new LiteralInline(literal.Content));
                }
            }

            fromInclusive = overlapEndExclusive;
            if (overlapEndExclusive >= literalEndExclusive)
            {
                literalIndex++;
            }
        }
    }

    /// <summary>Formats the exact title for a wikilink that resolved to no note (Spec §6.5 rule 4).</summary>
    private static readonly CompositeFormat UnresolvedNoteTitleFormat =
        CompositeFormat.Parse("No note named \"{0}\". Click to create it.");

    /// <summary>Formats the exact title for a wikilink that tied between several notes (Spec §6.5 rule 3, D9 item 39).</summary>
    private static readonly CompositeFormat AmbiguousNoteTitleFormat =
        CompositeFormat.Parse("Several notes match \"{0}\"; showing the closest.");

    /// <summary>
    /// Builds the <c>library-ref</c> <see cref="LinkInline"/> for one parsed <paramref name="link"/>, or - when
    /// <paramref name="library"/> declines to resolve it at all (the Library disabled) - a plain literal holding
    /// <paramref name="rawToken"/> exactly as authored, so the token is neither linked nor altered.
    /// </summary>
    /// <param name="library">The resolver for wikilinks.</param>
    /// <param name="from">The Library note the wikilink was written in.</param>
    /// <param name="link">The parsed wikilink.</param>
    /// <param name="rawToken">The wikilink's exact raw source text, used only when <paramref name="library"/> returns <see langword="null"/>.</param>
    private static Inline CreateWikiLinkInline(ILibraryNoteResolver library, LibraryPath from, WikiLink link, string rawToken)
    {
        LibraryReference? reference = library.ResolveWikiLink(from, link);
        if (reference is null)
        {
            return new LiteralInline(rawToken);
        }

        string text = link.Alias ?? (link.Heading is null ? link.Target : string.Concat(link.Target, "#", link.Heading));
        string? title = !reference.Exists
            ? string.Format(CultureInfo.InvariantCulture, MarkdownRenderer.UnresolvedNoteTitleFormat, link.Target)
            : reference.IsAmbiguous
                ? string.Format(CultureInfo.InvariantCulture, MarkdownRenderer.AmbiguousNoteTitleFormat, link.Target)
                : null;

        LinkInline result = new(MarkdownRenderer.BuildLibraryHref(reference.RootId, reference.RelativePath), title ?? string.Empty);
        result.AppendChild(new LiteralInline(text));
        result.GetAttributes().AddClass("library-ref");
        if (!reference.Exists)
        {
            result.GetAttributes().AddClass("library-ref-missing");
        }

        return result;
    }

    /// <summary>Builds a <c>?library=</c> href: the root id, then the relative path with each <c>/</c>-separated
    /// segment individually percent-encoded (so a literal <c>/</c> stays a path separator; Spec §6.6). The
    /// heading, when the wikilink had one, is never included - v1 links to the file only.</summary>
    /// <param name="rootId">The Library root id.</param>
    /// <param name="relativePath">The forward-slash relative path from the root.</param>
    private static string BuildLibraryHref(string rootId, string relativePath) =>
        MarkdownRenderer.BuildRootRelativeHref("?library=", rootId, relativePath);

    /// <summary>Builds a <c>/library-files/</c> href (Task 12.8's endpoint) for an image: the root id, then the
    /// relative path with each <c>/</c>-separated segment individually percent-encoded.</summary>
    /// <param name="rootId">The Library root id.</param>
    /// <param name="relativePath">The forward-slash relative path from the root.</param>
    private static string BuildLibraryFilesHref(string rootId, string relativePath) =>
        MarkdownRenderer.BuildRootRelativeHref("/library-files/", rootId, relativePath);

    /// <summary>Shared by <see cref="BuildLibraryHref"/> and <see cref="BuildLibraryFilesHref"/>: <paramref name="prefix"/>,
    /// then the root id, then the relative path with each segment individually percent-encoded.</summary>
    /// <param name="prefix">The href prefix, including any trailing separator or <c>=</c>.</param>
    /// <param name="rootId">The Library root id.</param>
    /// <param name="relativePath">The forward-slash relative path from the root.</param>
    private static string BuildRootRelativeHref(string prefix, string rootId, string relativePath) =>
        string.Concat(prefix, rootId, "/", string.Join('/', relativePath.Split('/').Select(Uri.EscapeDataString)));

    /// <summary>
    /// The prefix marking a <c>/library-files/</c> href this render resolved for an image, stripped by
    /// <see cref="RewriteUrl"/> before it reaches the page; see <see cref="RewriteRelativeLinks"/>.
    /// </summary>
    private const string LibraryFilesPrefix = "/library-files/";

    /// <summary>
    /// Resolves <see cref="Markdig.Renderers.HtmlRenderer.LinkRewriter"/> for one URL: a
    /// <paramref name="trustToken"/>-marked href (an image this render resolved through
    /// <see cref="RewriteRelativeLinks"/>) is admitted for the <c>/library-files/</c> route - after
    /// stripping the marker and a syntactic re-check (corrections-B6 item 30) - and anything else falls
    /// through to <see cref="IsSafe"/>, which never admits that route: the identical URL text written as
    /// a plain <c>a href</c> carries no marker and still renders <c>#</c>.
    /// </summary>
    /// <param name="url">The candidate link target, as written by <see cref="Markdig.Renderers.HtmlRenderer"/>.</param>
    /// <param name="trustToken">This render call's marker.</param>
    private static string RewriteUrl(string? url, string trustToken)
    {
        if (url is not null && url.StartsWith(trustToken, StringComparison.Ordinal))
        {
            string href = url[trustToken.Length..];
            return href.StartsWith(MarkdownRenderer.LibraryFilesPrefix, StringComparison.Ordinal) &&
                MarkdownRenderer.IsSafeLibraryFilesHref(href[MarkdownRenderer.LibraryFilesPrefix.Length..])
                    ? href
                    : "#";
        }

        return MarkdownRenderer.IsSafe(url) ? url! : "#";
    }

    /// <summary>
    /// Whether <paramref name="url"/> may reach the rendered page unchanged: an <c>http:</c>,
    /// <c>https:</c> or <c>mailto:</c> link, or a bare Task reference - <c>/tasks/item/</c> followed
    /// by exactly a valid <see cref="TaskId"/>'s canonical text and nothing else (Spec §13.13.2
    /// warning). The <c>/library-files/</c> route is never admitted here - only <see cref="RewriteUrl"/>'s
    /// marker check admits it, for an image (corrections-B6 item 30). Anything else, including a
    /// path-traversal attempt dressed up as one, is rewritten to <c>#</c>.
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

        const string LibraryQueryPrefix = "?library=";
        if (url.StartsWith(LibraryQueryPrefix, StringComparison.Ordinal))
        {
            return MarkdownRenderer.IsSafeLibraryHref(url[LibraryQueryPrefix.Length..]);
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

    /// <summary>
    /// Whether the text after <c>?library=</c> is exactly the admitted shape (corrections-B5 item 23): a raw
    /// <c>&amp;</c> or <c>#</c> would start a second query parameter or a fragment, so either refuses outright
    /// before decoding; once decoded, the value is <c>rootId/relative</c> with the root id matching
    /// <see cref="LibraryRootIdRegex"/> and no <c>.</c>, <c>..</c>, empty or backslash-bearing segment. This
    /// check stays syntactic - the click handler re-resolves the path for real - so it never touches the disk.
    /// </summary>
    /// <param name="encodedValue">The text of the query string after <c>?library=</c>.</param>
    private static bool IsSafeLibraryHref(string encodedValue)
    {
        if (encodedValue.IndexOfAny(['&', '#']) >= 0)
        {
            return false;
        }

        return MarkdownRenderer.IsSafeRootRelativeValue(encodedValue);
    }

    /// <summary>
    /// Whether the text after <c>/library-files/</c> is exactly the admitted shape (corrections-B6 item 30): a
    /// syntactic mirror of <see cref="IsSafeLibraryHref"/> for the image-only route - decode first, then
    /// <c>rootId/relative</c> with the root id matching <see cref="LibraryRootIdRegex"/> and no <c>.</c>,
    /// <c>..</c>, empty or backslash-bearing segment. Called only when <paramref name="encodedValue"/>'s full
    /// href is already known to be one this render generated for an image (<see cref="IsSafe"/>'s set check).
    /// </summary>
    /// <param name="encodedValue">The text of the URL after <c>/library-files/</c>.</param>
    private static bool IsSafeLibraryFilesHref(string encodedValue) =>
        MarkdownRenderer.IsSafeRootRelativeValue(encodedValue);

    /// <summary>Shared by <see cref="IsSafeLibraryHref"/> and <see cref="IsSafeLibraryFilesHref"/>: decodes
    /// <paramref name="encodedValue"/> and checks it is <c>rootId/relative</c> with the root id matching
    /// <see cref="LibraryRootIdRegex"/> and no <c>.</c>, <c>..</c>, empty or backslash-bearing segment. This
    /// check stays syntactic - the click handler (or the <c>/library-files/</c> endpoint itself) re-resolves the
    /// path for real - so it never touches the disk.</summary>
    /// <param name="encodedValue">The text of the URL after its <c>?library=</c> or <c>/library-files/</c> prefix.</param>
    private static bool IsSafeRootRelativeValue(string encodedValue)
    {
        string decoded = Uri.UnescapeDataString(encodedValue);
        if (decoded.Contains('\\', StringComparison.Ordinal))
        {
            return false;
        }

        string[] segments = decoded.Split('/');
        if (segments.Length < 2 || !MarkdownRenderer.LibraryRootIdRegex().IsMatch(segments[0]))
        {
            return false;
        }

        for (int i = 1; i < segments.Length; i++)
        {
            if (segments[i] is "" or "." or "..")
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Matches a Library root id: lower-case letters, digits and hyphens only.</summary>
    [GeneratedRegex("^[a-z0-9-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex LibraryRootIdRegex();
}
