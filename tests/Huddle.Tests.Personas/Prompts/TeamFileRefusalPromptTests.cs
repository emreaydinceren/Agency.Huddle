using Agency.Huddle.App.Prompts;

namespace Agency.Huddle.Tests.Prompts;

/// <summary>Pins the <c>systemPrompt.teamFileRefusal</c> Prompt, the reason <c>HumanTeamFilesGuard</c> gives (Team Playbooks spec §7.6).</summary>
public sealed class TeamFileRefusalPromptTests
{
    /// <summary>The default is the spec's sentence word for word.</summary>
    [Fact]
    public void TeamFileRefusal_DefaultIsTheSpecText()
    {
        Assert.Equal(
            "This file is the Human's. Propose a change in a retro, or ask the Human.",
            PromptCatalog.Get("systemPrompt.teamFileRefusal").Default);
    }

    /// <summary>The refusal carries no path, so the Prompt declares no placeholder and applies at the next session, as the AGENTS.md refusal does.</summary>
    [Fact]
    public void TeamFileRefusal_HasItsLabelAndNoPlaceholders_AndAppliesNextSession()
    {
        PromptDefinition prompt = PromptCatalog.Get("systemPrompt.teamFileRefusal");

        Assert.Equal("Team file write refused", prompt.Label);
        Assert.Equal(PromptTiming.NextSession, prompt.Timing);
        Assert.Empty(prompt.Placeholders);
        Assert.Empty(prompt.RequiredPlaceholders);
        Assert.False(prompt.Default.Contains("mcp__team__", StringComparison.Ordinal));
    }
}
