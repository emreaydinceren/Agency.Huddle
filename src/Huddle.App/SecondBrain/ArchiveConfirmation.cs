using System.Globalization;

namespace Agency.Huddle.App.SecondBrain;

/// <summary>What archiving a Team takes off the Boards, shown before the Human confirms (T16).</summary>
/// <param name="OpenTasks">The Team's Tasks that are not closed.</param>
/// <param name="NeedsYou">Of those, the ones waiting on the Human.</param>
/// <param name="NoTeamNames">The members whose only Team this is, computed before any label is removed.</param>
internal sealed record ArchiveConfirmation(int OpenTasks, int NeedsYou, IReadOnlyList<string> NoTeamNames)
{
    /// <summary>The settled confirmation text; the second sentence appears only when an Agent would be left with no Team.</summary>
    /// <param name="team">The Team's display name.</param>
    public string Text(string team)
    {
        string text = string.Create(
            CultureInfo.InvariantCulture,
            $"Archive {team}? {this.OpenTasks} open Tasks and {this.NeedsYou} waiting on you will leave the Boards.");
        return this.NoTeamNames.Count == 0
            ? text
            : text + " These Agents will have no Team: " + string.Join(", ", this.NoTeamNames) + ".";
    }
}
