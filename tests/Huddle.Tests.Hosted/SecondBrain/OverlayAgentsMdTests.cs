using Agency.Huddle.App;

namespace Agency.Huddle.Tests.SecondBrain;

/// <summary>Pins Team Playbooks spec §10: three conduct lines left the shipped Huddle-wide AGENTS.md, because the Member Playbook now holds them.</summary>
public sealed class OverlayAgentsMdTests
{
    private const string ResourceName = "Overlays/second-brain/AGENTS.md";

    /// <summary>The "@all" line, the "ask with options" line and the "look up a fact" line are gone from the shipped AGENTS.md.</summary>
    [Theory]
    [InlineData("Write \"@all\" only when you need every other agent to answer")]
    [InlineData("ask with options they can tap instead")]
    [InlineData("Don't ask for a fact you can look up or infer")]
    public void Shipped_NoLongerHoldsTheLine(string fragment)
    {
        string text = Embedded();

        Assert.DoesNotContain(fragment, text, StringComparison.Ordinal);
    }

    /// <summary>The lines around the removed ones stay, so the section is trimmed and not emptied.</summary>
    [Fact]
    public void Shipped_KeepsTheNeighbouringLines()
    {
        string text = Embedded();

        Assert.Contains("- When a decision is reversed, replace it and note briefly that it changed.", text, StringComparison.Ordinal); // contains-ok: exact rule sentence of model-facing text
        Assert.Contains("- Name output files for what they are", text, StringComparison.Ordinal); // contains-ok: exact rule sentence of model-facing text
    }

    /// <summary>Reads the embedded default AGENTS.md.</summary>
    /// <returns>Its text.</returns>
    private static string Embedded()
    {
        using Stream stream = EmbeddedResources.Open(typeof(Agency.Huddle.App.SecondBrain.DefaultAgentsMdService).Assembly, ResourceName);
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
