namespace Agency.Huddle.Tests.Prompts;

using Agency.Huddle.App.Prompts;

/// <summary>Pins the <c>systemPrompt.agentsMdRefusal</c> Prompt, the settled text T5 (Spec §6.6, corrections-7 §7.1 item 7).</summary>
public sealed class AgentsMdRefusalPromptTests
{
    /// <summary>The default is T5 word for word.</summary>
    [Fact]
    public void AgentsMdRefusal_DefaultIsT5()
    {
        Assert.Equal(
            "AGENTS.md is the Human's file. Propose a change in a retrospective instead.",
            PromptCatalog.Get("systemPrompt.agentsMdRefusal").Default);
    }

    /// <summary>T5 carries no path, so the Prompt declares no placeholder and applies at the next session.</summary>
    [Fact]
    public void AgentsMdRefusal_HasNoPlaceholders_AndAppliesNextSession()
    {
        PromptDefinition prompt = PromptCatalog.Get("systemPrompt.agentsMdRefusal");

        Assert.Equal(PromptTiming.NextSession, prompt.Timing);
        Assert.Empty(prompt.Placeholders);
        Assert.Empty(prompt.RequiredPlaceholders);
    }
}
