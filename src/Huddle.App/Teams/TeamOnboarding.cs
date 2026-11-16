using Agency.Huddle.App.Library;
using Agency.Huddle.App.Prompts;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Seeds a manager's first Turn with the onboarding steps of its Team's <c>AGENTS.md</c> (PR-5, Spec §Hiring), posted in
/// the manager's one-to-one scoped to the Team, the Room the Team page's Message button opens.
/// The <c>onboarded: true</c> line in <c>_team.md</c> makes the post happen once: a restart or a second start finds
/// the marker and posts nothing.
/// </summary>
/// <param name="folders">Locates the Team folder and its <c>_team.md</c>.</param>
/// <param name="prompts">Supplies the onboarding prompt's current text.</param>
/// <param name="poster">Delivers the message to the manager.</param>
internal sealed class TeamOnboarding(ITeamFolders folders, IPromptSource prompts, IOnboardingPoster poster)
{
    /// <summary>Posts the onboarding prompt in the manager's Team one-to-one unless the Team was already onboarded; a refused post leaves the marker unset.</summary>
    /// <param name="team">The Team's name.</param>
    /// <param name="managerName">The manager Agent's Name.</param>
    /// <param name="ct">Cancels the post.</param>
    /// <returns><see langword="true"/> when the prompt was posted and the marker written.</returns>
    public async Task<bool> PostIfDueAsync(string team, string managerName, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);
        ArgumentException.ThrowIfNullOrWhiteSpace(managerName);

        TeamFile? file = folders.ReadTeamFile(team);
        if (file is null || file.Onboarded)
        {
            return false;
        }

        string? steps = ReadSteps(team);
        if (steps is null)
        {
            return false;
        }

        string text = prompts.Render("turn.onboarding", new Dictionary<string, string> { ["{{steps}}"] = steps });
        if (!await poster.PostToTeamOneToOneAsync(team, managerName, text, ct).ConfigureAwait(false))
        {
            return false;
        }

        return folders.UpdateTeamFile(team, static current => current with { Onboarded = true });
    }

    private string? ReadSteps(string team) =>
        TeamProcessFile.Read(folders, team) is { } text ? ProcessDocument.OnboardingSteps(text) : null;
}
