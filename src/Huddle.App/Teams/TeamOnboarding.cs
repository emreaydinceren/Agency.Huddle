using Agency.Huddle.App.Library;
using Agency.Huddle.App.Prompts;

namespace Agency.Huddle.App.Teams;

/// <summary>Delivers a message as the Human to a manager's direct Room, so it becomes the manager's next Turn.</summary>
internal interface IOnboardingPoster
{
    /// <summary>Posts <paramref name="text"/> to the manager's Room.</summary>
    /// <param name="managerName">The manager Teammate's Name.</param>
    /// <param name="text">The message.</param>
    /// <param name="ct">Cancels the post.</param>
    /// <returns><see langword="true"/> when the message was posted; <see langword="false"/> when the manager or its Room could not be reached.</returns>
    Task<bool> PostAsync(string managerName, string text, CancellationToken ct);
}

/// <summary>
/// Seeds a manager's first Turn with the onboarding steps of its Team's <c>Process.md</c> (PR-5, Spec §Hiring).
/// The <c>onboarded: true</c> line in <c>_team.md</c> makes the post happen once: a restart or a second start finds
/// the marker and posts nothing.
/// </summary>
/// <param name="folders">Locates the Team folder and its <c>_team.md</c>.</param>
/// <param name="prompts">Supplies the onboarding prompt's current text.</param>
/// <param name="poster">Delivers the message to the manager.</param>
internal sealed class TeamOnboarding(ITeamFolders folders, IPromptSource prompts, IOnboardingPoster poster)
{
    /// <summary>Posts the onboarding prompt to the manager unless the Team was already onboarded.</summary>
    /// <param name="team">The Team's name.</param>
    /// <param name="managerName">The manager Teammate's Name.</param>
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
        if (!await poster.PostAsync(managerName, text, ct).ConfigureAwait(false))
        {
            return false;
        }

        return folders.UpdateTeamFile(team, static current => current with { Onboarded = true });
    }

    private string? ReadSteps(string team)
    {
        LibraryResult<LibraryPath> folder = folders.FindTeam(team);
        if (folder.Value is null)
        {
            return null;
        }

        string path = Path.Combine(folder.Value.FullPath, TeamPaths.ProcessFileName);
        try
        {
            return File.Exists(path) ? ProcessDocument.OnboardingSteps(File.ReadAllText(path)) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
