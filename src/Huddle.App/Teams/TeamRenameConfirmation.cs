using System.Globalization;
using Agency.Huddle.App.Components.Shared.Dialogs;

namespace Agency.Huddle.App.Teams;

/// <summary>The words of the rename confirmation (Team Rename Spec §7), built from what the rename touches.</summary>
internal static class TeamRenameConfirmation
{
    private const int NamedAtMost = 3;

    /// <summary>The confirmation's title, a question.</summary>
    /// <param name="kind">A Team or a Project.</param>
    /// <param name="oldName">The current name.</param>
    /// <param name="newName">The new name.</param>
    public static string Title(TeamRenameKind kind, string oldName, string newName) =>
        kind == TeamRenameKind.Project ? $"Rename project {oldName} to {newName}?" : $"Rename {oldName} to {newName}?";

    /// <summary>The sentence above the points.</summary>
    /// <param name="kind">A Team or a Project.</param>
    public static string Message(TeamRenameKind kind) =>
        $"Huddle moves the {Noun(kind)}'s folder and updates its Rooms, Tasks, Requests and saved Views. Some things can go wrong or stay behind:";

    /// <summary>The points of what can go wrong or stay behind, in order.</summary>
    /// <param name="kind">A Team or a Project.</param>
    /// <param name="oldName">The current name.</param>
    /// <param name="memberCount">How many members the Team has; ignored for a Project.</param>
    /// <param name="midTurnNames">The members whose Agent is in the middle of a Turn; ignored for a Project.</param>
    /// <param name="team">A Project's Team; ignored for a Team.</param>
    public static IReadOnlyList<ConfirmPoint> Points(TeamRenameKind kind, string oldName, int memberCount, IReadOnlyList<string> midTurnNames, string? team = null)
    {
        ArgumentNullException.ThrowIfNull(midTurnNames);

        string noun = Noun(kind);
        string path = kind == TeamRenameKind.Project ? $"Teams/{team}/{oldName}" : $"Teams/{oldName}";
        List<ConfirmPoint> points = [];
        if (kind == TeamRenameKind.Project)
        {
            points.Add(new ConfirmPoint("No Agent restarts.", " Agents in its Rooms keep their conversations."));
        }
        else if (memberCount > 0)
        {
            points.Add(RestartPoint(memberCount, midTurnNames));
        }

        points.Add(new ConfirmPoint(
            "Agents are told on their next Turn, not now.",
            $" Until then an Agent may use the old name. Huddle catches most of that, but an Agent that writes a file to the old folder brings it back as an empty {noun}."));
        points.Add(new ConfirmPoint("Agents' own notes keep the old name", " until each Agent updates them."));
        points.Add(new ConfirmPoint("Links outside Huddle break.", $" Bookmarks, scripts or other programs that use {path} need updating."));
        points.Add(new ConfirmPoint("This can't be undone in one step,", $" and {oldName} stays reserved. Renaming back is another rename, with the same effects."));
        return points;
    }

    private static string Noun(TeamRenameKind kind) => kind == TeamRenameKind.Project ? "Project" : "Team";

    private static ConfirmPoint RestartPoint(int memberCount, IReadOnlyList<string> midTurnNames)
    {
        string lead = memberCount == 1
            ? "1 member restarts and starts a new conversation."
            : string.Create(CultureInfo.InvariantCulture, $"{memberCount} members restart and start new conversations.");
        string text = " Each one keeps a summary of recent Messages but loses its working context.";
        if (midTurnNames.Count > 0)
        {
            string verbs = midTurnNames.Count == 1 ? "is" : "are";
            string loses = midTurnNames.Count == 1 ? "loses" : "lose";
            text += $" {JoinNames(midTurnNames)} {verbs} in the middle of a Turn and {loses} that Turn's work.";
        }

        return new ConfirmPoint(lead, text);
    }

    private static string JoinNames(IReadOnlyList<string> names)
    {
        if (names.Count > NamedAtMost)
        {
            int others = names.Count - NamedAtMost;
            string rest = others == 1 ? "1 other" : string.Create(CultureInfo.InvariantCulture, $"{others} others");
            string named = string.Join(", ", names.Take(NamedAtMost));
            return $"{named} and {rest}";
        }

        string allButLast = string.Join(", ", names.Take(names.Count - 1));
        return names.Count == 1 ? names[0] : $"{allButLast} and {names[^1]}";
    }
}
