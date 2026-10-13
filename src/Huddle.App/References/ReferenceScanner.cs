using System.Text.RegularExpressions;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Contracts;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Agency.Huddle.App.References;

/// <summary>
/// Finds the references a Message's text names (spec §8.2 step 1): Task ids outside code, Room and Team links,
/// and Library paths in code spans or in plain text. Pure: it does not check that a reference exists, which
/// the resolver does. Its caps follow D-19.
/// </summary>
internal static class ReferenceScanner
{
    /// <summary>Shares the chat Markdown pipeline with the renderer, so the scanner sees what the renderer sees.</summary>
    private static readonly MarkdownPipeline Pipeline = MarkdownRenderer.CreateBuilder().Build();

    /// <summary>Finds the references in a Message's text, in first-seen order, deduplicated and cut per family.</summary>
    /// <param name="text">The Message's text, as sent.</param>
    /// <returns>The candidates: at most <see cref="ReferenceLimits.MaxCandidates"/> references and as many Library files.</returns>
    public static IReadOnlyList<ReferenceCandidate> Scan(string text)
    {
        MarkdownDocument document = Markdown.Parse(text, ReferenceScanner.Pipeline);
        List<ReferenceCandidate> found = [];

        foreach (List<LiteralInline> run in ReferenceScanner.LiteralRuns(document))
        {
            string content = string.Concat(run.Select(literal => literal.Content.ToString()));
            int start = run[0].Span.Start;

            foreach (Match match in TaskIdPattern.Candidate().Matches(content))
            {
                if (TaskId.TryParse(match.Value, out TaskId id))
                {
                    found.Add(new ReferenceCandidate(ReferenceKind.Task, id.ToString(), start + match.Index));
                }
            }

            foreach ((int Index, int Length, string Path) hit in LibraryPathPatterns.Find(content))
            {
                found.Add(new ReferenceCandidate(ReferenceKind.LibraryFile, hit.Path, start + hit.Index));
            }
        }

        foreach (LinkInline link in document.Descendants<LinkInline>())
        {
            if (link.IsImage || link.Url is not { } url)
            {
                continue;
            }

            if (ReferenceText.TryParseRoomHref(url, out string? roomId))
            {
                found.Add(new ReferenceCandidate(ReferenceKind.Room, roomId, link.Span.Start));
            }
            else if (ReferenceText.TryParseTeamHref(url, out string? teamName))
            {
                found.Add(new ReferenceCandidate(ReferenceKind.Team, teamName, link.Span.Start));
            }
        }

        foreach (CodeInline code in document.Descendants<CodeInline>())
        {
            if (LibraryPathPatterns.TryMatchWhole(code.Content, out string? path))
            {
                found.Add(new ReferenceCandidate(ReferenceKind.LibraryFile, path, code.Span.Start));
            }
        }

        return ReferenceScanner.Limit(found);
    }

    /// <summary>
    /// Groups the plain-text literals into runs: consecutive literals in one container with no gap between them,
    /// so a path or id that the parser split across nodes is still read whole. Literals inside a link are left out.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <returns>The runs, in document order.</returns>
    private static List<List<LiteralInline>> LiteralRuns(MarkdownDocument document)
    {
        List<List<LiteralInline>> runs = [];
        LiteralInline? previous = null;

        foreach (LiteralInline literal in document.Descendants<LiteralInline>())
        {
            if (ReferenceScanner.HasLinkAncestor(literal))
            {
                previous = null;
                continue;
            }

            bool continues = previous is not null
                && ReferenceEquals(previous.Parent, literal.Parent)
                && previous.Span.End + 1 == literal.Span.Start;
            if (!continues)
            {
                runs.Add([]);
            }

            runs[^1].Add(literal);
            previous = literal;
        }

        return runs;
    }

    /// <summary>Whether an inline sits inside a link, where a Task id or path is the link's text, not a reference.</summary>
    /// <param name="inline">The inline to check.</param>
    /// <returns>True when a link is an ancestor.</returns>
    private static bool HasLinkAncestor(Inline inline)
    {
        for (ContainerInline? parent = inline.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is LinkInline)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Removes repeats (kept at their first place) and stops each family at the hard ceiling
    /// <see cref="ReferenceLimits.MaxCandidates"/>. The real caps (D-19) apply to what resolves, in
    /// <see cref="ReferenceResolver.Cap"/>: a word like UTF-8 that names nothing must not use one up.
    /// </summary>
    /// <param name="found">The candidates in the order they were found.</param>
    /// <returns>The kept candidates, in position order.</returns>
    private static List<ReferenceCandidate> Limit(List<ReferenceCandidate> found)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        List<ReferenceCandidate> kept = [];
        int referenceCount = 0;
        int libraryCount = 0;

        foreach (ReferenceCandidate candidate in found.OrderBy(candidate => candidate.Position))
        {
            string key = candidate.Kind is ReferenceKind.Team or ReferenceKind.LibraryFile
                ? candidate.Key.ToUpperInvariant()
                : candidate.Key;
            if (!seen.Add(string.Concat(candidate.Kind.ToString(), "\u001F", key)))
            {
                continue;
            }

            if (candidate.Kind == ReferenceKind.LibraryFile)
            {
                if (libraryCount >= ReferenceLimits.MaxCandidates)
                {
                    continue;
                }

                libraryCount++;
            }
            else
            {
                if (referenceCount >= ReferenceLimits.MaxCandidates)
                {
                    continue;
                }

                referenceCount++;
            }

            kept.Add(candidate);
        }

        return kept;
    }
}
