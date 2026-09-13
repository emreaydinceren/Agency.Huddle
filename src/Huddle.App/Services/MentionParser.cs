using Agency.Huddle.App.Data;

namespace Agency.Huddle.App.Services;

/// <summary>
/// Finds the Members Mentioned in a Message.
/// </summary>
/// <remarks>
/// This used to be one regex that read a Mention's shape straight out of the text. That stopped
/// working when a Name gained the right to contain spaces, because "@Emily Lee" and "@Emily"
/// followed by the word "Lee" are the same characters: no pattern can tell them apart without
/// knowing who is actually in the Room. So the member list — which this method has always been
/// handed — decides where a Name ends, and the text is only asked whether it starts with one.
/// </remarks>
public static class MentionParser
{
    /// <summary>
    /// Parses <paramref name="text"/> for Mentions of <paramref name="members"/> by Name only. Delegates
    /// to <see cref="Parse(string, IReadOnlyList{User}, IReadOnlyList{MentionAlias})"/> with an empty
    /// Alias list, so the ~20 existing call sites and tests that only ever dealt with Names keep working
    /// exactly as before.
    /// </summary>
    /// <param name="text">The Message text to scan.</param>
    /// <param name="members">The Room's Members. A Mention can only ever resolve to one of these.</param>
    public static IReadOnlyList<User> Parse(string text, IReadOnlyList<User> members) => Parse(text, members, []);

    /// <summary>
    /// Parses <paramref name="text"/> for Mentions of <paramref name="members"/>, matched either by a
    /// Member's Name or by a Persona's Alias.
    /// </summary>
    /// <param name="text">The Message text to scan.</param>
    /// <param name="members">The Room's Members. A Mention can only ever resolve to one of these.</param>
    /// <param name="aliases">
    /// Every Alias currently known, library-wide - typically <see cref="Agency.Huddle.App.Acp.PersonaStore"/>'s
    /// full set via <see cref="IMentionAliasSource"/>, unfiltered by this Room's membership. An Alias
    /// whose owning Name is not one of <paramref name="members"/> contributes no candidate: Aliases are
    /// unique across the whole Team, but a Mention can only ever resolve to someone actually in this Room.
    /// </param>
    public static IReadOnlyList<User> Parse(string text, IReadOnlyList<User> members, IReadOnlyList<MentionAlias> aliases)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(aliases);

        // Longest handle first, so "@Emily Lee" resolves to "Emily Lee" and not to "Emily" in a Room
        // that holds both. The longer reading is the safer default: a writer who meant Emily had no
        // reason to type her surname after her Name. Names and Aliases are sorted together in ONE
        // list, not scanned in two separate passes: a two-pass approach (all Names first, Aliases only
        // as a fallback) would let a short Name win here whenever it happens to sit at a real word
        // boundary - exactly the "Emily"/"Emily Lee" trap, just triggered by an Alias instead of a
        // second Name.
        //
        // Names are concatenated BEFORE Aliases, deliberately: LINQ's OrderByDescending is a stable
        // sort, so when a Name and an Alias tie in length, whichever one appears first in the
        // un-sorted sequence wins that tie. Swap the two Concat operands below and an equal-length
        // Alias would start silently beating a Name, with no compiler or analyser signal -
        // Parse_NameBeatsAnEqualLengthAlias in MentionParserTests pins this ordering so a future edit
        // cannot make that swap unnoticed.
        var candidates = members
            .Where(member => member.Name.Length > 0)
            .Select(member => new Candidate(member.Name, member))
            .Concat(ResolveAliasCandidates(members, aliases))
            .OrderByDescending(candidate => candidate.Handle.Length)
            .ToList();

        var result = new List<User>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '@' || (i > 0 && IsBlockedBeforeAt(text[i - 1])))
            {
                continue;
            }

            foreach (var candidate in candidates)
            {
                var start = i + 1;
                var end = start + candidate.Handle.Length;

                if (end > text.Length ||
                    string.Compare(text, start, candidate.Handle, 0, candidate.Handle.Length, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    continue;
                }

                // A Mention ends at a word boundary, so "@echo" never Mentions a Member called
                // "ech". Note that a space is NOT a boundary blocker: whether the space after
                // "Emily" continues her Name is precisely what the member list just answered.
                if (end < text.Length && IsNameCharacter(text[end]))
                {
                    continue;
                }

                if (seenIds.Add(candidate.Member.Id))
                {
                    result.Add(candidate.Member);
                }

                // Resume past the handle just consumed (the loop's i++ steps onto `end`), so the
                // words inside a multi-word Name cannot be re-read as a Mention of someone else.
                // This is a deliberate scan-advance, not an accidental mutation of the loop's stop
                // condition: without it, a multi-word Name like "Emily Lee" would let "Lee" be
                // re-scanned as a possible Mention of a different Member.
#pragma warning disable S127 // Deliberate scan-advance (see comment above), not the accidental stop-condition mutation the rule guards against.
                i = end - 1;
#pragma warning restore S127
                break;
            }
        }

        return result;
    }

    // '@' itself is here so "you@@echo" is not a Mention, and '_' and the alphanumerics so an
    // email address ("me@example.com") is not one either — the character before the '@' shows that
    // the '@' is joining two things rather than introducing a Name. '-' is deliberately absent:
    // "see-@echo" is a Mention.
    private static bool IsBlockedBeforeAt(char c)
    {
        return char.IsAsciiLetterOrDigit(c) || c == '_' || c == '@';
    }

    private static bool IsNameCharacter(char c)
    {
        return char.IsAsciiLetterOrDigit(c) || c == '_' || c == '-';
    }

    /// <summary>
    /// Projects every Alias whose owning Name matches one of <paramref name="members"/> (compared
    /// <see cref="StringComparison.OrdinalIgnoreCase"/>) into a <see cref="Candidate"/> pointing at that
    /// Member. An Alias belonging to nobody in this Room yields nothing - <c>PersonaIndex</c>'s
    /// uniqueness guarantee holds library-wide, not Room-wide, so an Alias can easily name someone who
    /// simply is not here.
    /// </summary>
    private static IEnumerable<Candidate> ResolveAliasCandidates(IReadOnlyList<User> members, IReadOnlyList<MentionAlias> aliases)
    {
        foreach (var alias in aliases)
        {
            if (alias.Alias.Length == 0)
            {
                continue;
            }

            var owner = members.FirstOrDefault(member => string.Equals(member.Name, alias.Name, StringComparison.OrdinalIgnoreCase));
            if (owner is not null)
            {
                yield return new Candidate(alias.Alias, owner);
            }
        }
    }

    /// <summary>One handle - a Member's Name or a Persona's Alias - paired with the Member it resolves to, for the candidate scan.</summary>
    private sealed record Candidate(string Handle, User Member);
}