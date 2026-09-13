namespace Agency.Huddle.App.Components.Pages;

using Agency.Huddle.App.Acp;

/// <summary>
/// One heading's worth of tiles on the Teammates directory page: every Persona whose
/// <see cref="PersonaEntry.Teams"/> names this Team, or every Persona with no Team at all when
/// <see cref="Heading"/> is <see cref="TeammateGrouping.NoTeamHeading"/>.
/// </summary>
/// <param name="Heading">The Team name shown above this group's tiles.</param>
/// <param name="Members">Every Persona belonging to this group, in <see cref="TeammateGrouping.Group"/>'s input order.</param>
public sealed record TeammateGroup(string Heading, IReadOnlyList<PersonaEntry> Members);

/// <summary>
/// Groups Personas into the headings <c>Teammates.razor</c> renders, and narrows them to one Team
/// when the page's filter has a choice selected. Pulled out of the page itself, as a pure function
/// with no dependency on Blazor at all, so the behaviours that matter here - a Persona's group comes
/// from its <see cref="PersonaEntry.Teams"/> field and never from which folder its file happens to
/// sit under, a Persona in two Teams appears under both, a Persona with none falls under
/// <see cref="NoTeamHeading"/>, and choosing a filter narrows the result to that one Team - can all
/// be proven by a plain unit test with no rendering harness needed at all. <c>HtmlRenderer</c> (the
/// only rendering test double this suite has, see <c>TeammatesRazorSourceTests</c>) cannot simulate
/// choosing an option in a live <c>&lt;select&gt;</c>, so this split is what makes the filter's
/// narrowing behaviour testable at all.
/// </summary>
public static class TeammateGrouping
{
    /// <summary>The heading a Persona with an empty <see cref="PersonaEntry.Teams"/> falls under.</summary>
    public const string NoTeamHeading = "No team";

    /// <summary>
    /// Builds one heading per Team in <paramref name="teams"/> - in the order given, so a caller
    /// passing the already-sorted <see cref="PersonaStore.Teams"/> gets sorted headings for free -
    /// each holding every entry whose <see cref="PersonaEntry.Teams"/> names it (case-insensitively),
    /// followed by a final <see cref="NoTeamHeading"/> group for every entry with no Team at all. A
    /// group with no members is omitted rather than rendered empty. When
    /// <paramref name="selectedTeam"/> is not blank, every entry is narrowed to that one Team first
    /// and at most one heading (that Team's own) is produced - an empty result when the Team
    /// currently has no members, rather than a group with nothing under it.
    /// </summary>
    /// <param name="entries">Every Persona that loaded cleanly.</param>
    /// <param name="teams">Every distinct Team name across all Personas, in the order headings should appear.</param>
    /// <param name="selectedTeam">The filter's current choice, or blank/<see langword="null"/> for "All teams".</param>
    public static IReadOnlyList<TeammateGroup> Group(
        IReadOnlyList<PersonaEntry> entries,
        IReadOnlyList<string> teams,
        string? selectedTeam)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(teams);

        if (!string.IsNullOrWhiteSpace(selectedTeam))
        {
            var narrowed = MembersOf(entries, selectedTeam);
            return narrowed.Count == 0 ? [] : [new TeammateGroup(selectedTeam, narrowed)];
        }

        var groups = new List<TeammateGroup>();

        foreach (var team in teams)
        {
            var members = MembersOf(entries, team);
            if (members.Count > 0)
            {
                groups.Add(new TeammateGroup(team, members));
            }
        }

        var noTeam = entries.Where(entry => entry.Teams.Count == 0).ToList();
        if (noTeam.Count > 0)
        {
            groups.Add(new TeammateGroup(NoTeamHeading, noTeam));
        }

        return groups;
    }

    /// <summary>Every entry whose <see cref="PersonaEntry.Teams"/> names <paramref name="team"/>, case-insensitively.</summary>
    private static List<PersonaEntry> MembersOf(IReadOnlyList<PersonaEntry> entries, string team) =>
        entries.Where(entry => entry.Teams.Contains(team, StringComparer.OrdinalIgnoreCase)).ToList();
}
