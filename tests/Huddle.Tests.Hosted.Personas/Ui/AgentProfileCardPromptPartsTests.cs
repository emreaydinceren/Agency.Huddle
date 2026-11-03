using Bunit;
using static Agency.Huddle.Tests.Ui.AgentProfileCardTestSupport;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Pins that the Agent card in View mode carries the "What this Agent reads" section (Spec section 6.5).</summary>
public sealed class AgentProfileCardPromptPartsTests
{
    /// <summary>An opened Agent card shows the section with its Persona text row and the Adapter note.</summary>
    [Fact]
    public async Task ViewCard_ShowsWhatThisAgentReads()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "You are the COO.");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");

        var section = cut.Find(".agent-profile-card-reads");
        Assert.Contains("What this Agent reads", section.TextContent, StringComparison.Ordinal);
        Assert.Contains("Persona text", section.QuerySelectorAll(".prompt-part-label").Select(e => e.TextContent.Trim()));
        Assert.StartsWith("Shown for ", section.QuerySelector(".prompt-parts-note")?.TextContent.Trim(), StringComparison.Ordinal);
    }
}
