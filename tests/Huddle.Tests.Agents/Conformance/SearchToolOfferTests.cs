using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// Whether the <c>search</c> tool reaches a real composed system prompt for a Persona that holds no Skill, under a prefixed
/// and an unprefixed Adapter profile, driven the same way as <see cref="TaskToolOfferTests"/>.
/// </summary>
public sealed class SearchToolOfferTests
{
    /// <summary>A Persona with no Skills on a prefixed profile is offered <c>mcp__team__search</c>.</summary>
    [Fact]
    public async Task PrefixedProfile_OffersSearchToAPersonaWithNoSkills()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.", Adapter: "claude");
        Dictionary<string, string?> additionalConfig = new(StringComparer.Ordinal)
        {
            ["Team:Acp:Adapters:0:Id"] = "claude",
            ["Team:Acp:Adapters:0:Command"] = "node",
            ["Team:Acp:Adapters:0:Args:0"] = "--mock-adapter-fixture",
            ["Team:Acp:Adapters:0:UsesToolNamePrefix"] = "true",
        };

        await using MockAdapterFixture fixture =
            await MockAdapterFixture.StartAsync(persona, additionalConfig, cancellationToken: ct);

        string appendedPrompt = await fixture.AppendedSystemPromptAsync(ct);

        // contains-ok: the appended prompt lists every offered tool; this test's intent is only that search is among them.
        Assert.Contains("mcp__team__search", appendedPrompt, StringComparison.Ordinal);
    }

    /// <summary>On an unprefixed profile the same Persona is offered <c>search</c> bare, and the prompt carries no <c>mcp__</c> prefix.</summary>
    [Fact]
    public async Task UnprefixedProfile_OffersSearchBare()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.", Adapter: "agency");
        Dictionary<string, string?> additionalConfig = new(StringComparer.Ordinal)
        {
            ["Team:Acp:Adapters:0:Id"] = "agency",
            ["Team:Acp:Adapters:0:Command"] = "node",
            ["Team:Acp:Adapters:0:Args:0"] = "--mock-adapter-fixture",
            ["Team:Acp:Adapters:0:UsesToolNamePrefix"] = "false",
        };

        await using MockAdapterFixture fixture =
            await MockAdapterFixture.StartAsync(persona, additionalConfig, cancellationToken: ct);

        string appendedPrompt = await fixture.AppendedSystemPromptAsync(ct);

        // contains-ok: the appended prompt lists every offered tool; the bare name is checked together with the no-prefix rule.
        Assert.Contains("search", appendedPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("mcp__", appendedPrompt, StringComparison.Ordinal);
    }
}
