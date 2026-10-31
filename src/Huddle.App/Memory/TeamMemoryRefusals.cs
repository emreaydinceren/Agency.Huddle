using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Memory;

/// <summary>The one wording of "you may not write this Team's Memory", shared by the <c>memory</c> tool and the file-tool guard.</summary>
internal static class TeamMemoryRefusals
{
    /// <summary>Only the Team's manager writes its Memory, or, without a manager, the Human does.</summary>
    /// <param name="prompts">Renders <c>systemPrompt.memoryManagerOnly</c> or <c>systemPrompt.memoryNoManager</c>.</param>
    /// <param name="team">The Team.</param>
    /// <returns>The refusal.</returns>
    public static string ManagerOnly(IPromptSource prompts, TeamSummary team)
    {
        ArgumentNullException.ThrowIfNull(prompts);
        ArgumentNullException.ThrowIfNull(team);

        return team.Manager is { } manager
            ? prompts.Say("systemPrompt.memoryManagerOnly", ("team", team.Name), ("manager", manager))
            : prompts.Say("systemPrompt.memoryNoManager", ("team", team.Name));
    }
}
