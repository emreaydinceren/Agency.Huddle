using System.Text;

namespace Agency.Huddle.App.Library;

/// <summary>
/// Rewrites the target inside a set of wikilinks (Spec §6.5 <i>Rename or move rewrite</i> step 2).
/// Only the target text changes; alias, heading and a leading <c>!</c> are kept, and every other
/// byte of the note stays as the writer left it (ADR-0028).
/// </summary>
internal static class WikiLinkRewriter
{
    /// <summary>
    /// Rewrites <paramref name="text"/>, replacing the target of every <see cref="WikiLink"/> in
    /// <paramref name="edits"/> with its new target. Edits are applied from the highest
    /// <see cref="WikiLink.Start"/> down, so an earlier edit's offsets never shift.
    /// </summary>
    /// <param name="text">The note's text, with <c>\n</c>-only newlines (Spec §6.4).</param>
    /// <param name="edits">The links to rewrite, each with its new target text.</param>
    /// <returns>
    /// <paramref name="text"/> unchanged (the same instance) when <paramref name="edits"/> is
    /// empty, otherwise the rewritten text.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// An edit's <see cref="WikiLink"/> no longer matches the text at its recorded position
    /// (stale positions - the caller must re-parse before rewriting), or two edits overlap or
    /// duplicate the same position.
    /// </exception>
    internal static string Rewrite(string text, IReadOnlyList<(WikiLink Link, string NewTarget)> edits)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(edits);

        if (edits.Count == 0)
        {
            return text;
        }

        List<(WikiLink Link, string NewTarget)> ordered = [.. edits];
        ordered.Sort((left, right) => right.Link.Start.CompareTo(left.Link.Start));

        WikiLinkRewriter.EnsureNoOverlap(ordered);

        StringBuilder builder = new(text);
        foreach ((WikiLink link, string newTarget) in ordered)
        {
            ArgumentNullException.ThrowIfNull(newTarget);

            if (!WikiLinkParser.TryGetTargetSpan(text, link, out int start, out int length))
            {
                throw new ArgumentException("The wikilink's recorded position no longer matches the text; re-parse before rewriting.", nameof(edits));
            }

            string styledTarget = WikiLinkRewriter.StyleTarget(link.Target, newTarget);
            builder.Remove(start, length);
            builder.Insert(start, styledTarget);
        }

        return builder.ToString();
    }

    /// <summary>Throws when two edits' <c>[Start, Start+Length)</c> spans overlap or coincide.</summary>
    private static void EnsureNoOverlap(List<(WikiLink Link, string NewTarget)> ordered)
    {
        for (int i = 0; i < ordered.Count; i++)
        {
            WikiLink first = ordered[i].Link;
            for (int j = i + 1; j < ordered.Count; j++)
            {
                WikiLink second = ordered[j].Link;
                if (first.Start < second.Start + second.Length && second.Start < first.Start + first.Length)
                {
                    throw new ArgumentException("Edits overlap or duplicate a wikilink position.", nameof(ordered));
                }
            }
        }
    }

    /// <summary>B5 item 2: a target the writer spelled with a known extension (<c>[[auth.md]]</c>) keeps that extension when rewritten; a bare target stays bare.</summary>
    private static string StyleTarget(string originalTarget, string newTarget)
    {
        if (!LibraryFileKinds.HasKnownExtension(originalTarget) || LibraryFileKinds.HasKnownExtension(newTarget))
        {
            return newTarget;
        }

        return newTarget + Path.GetExtension(originalTarget);
    }
}
