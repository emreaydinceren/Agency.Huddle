using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// D28, RS §6.9, §6.12: the resolved Adapter Profile's <see cref="AdapterProfile.SessionPerRoom"/>
/// selects which of the two truthful system-prompt texts a real session gets, driven through
/// <see cref="MockAdapterFixture"/> exactly as <see cref="ToolPrefixTests"/> drives the tool-prefix
/// choice.
/// </summary>
public sealed class SessionScopeConformanceTests
{
    /// <summary>
    /// A configured profile with <c>SessionPerRoom: true</c> delivers a prompt ending with
    /// <c>systemPrompt.roomSessions</c>'s wording, not <c>systemPrompt.sharedSession</c>'s.
    /// </summary>
    [Fact]
    public async Task ProfileSessionPerRoom_SelectsText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.", Adapter: "claude");
        Dictionary<string, string?> additionalConfig = new(StringComparer.Ordinal)
        {
            ["Team:Acp:Adapters:0:Id"] = "claude",
            ["Team:Acp:Adapters:0:Command"] = "node",
            ["Team:Acp:Adapters:0:Args:0"] = "--mock-adapter-fixture",
            ["Team:Acp:Adapters:0:UsesToolNamePrefix"] = "true",
            ["Team:Acp:Adapters:0:SessionPerRoom"] = "true",
        };

        await using MockAdapterFixture fixture =
            await MockAdapterFixture.StartAsync(persona, additionalConfig, cancellationToken: ct);

        string appendedPrompt = await fixture.AppendedSystemPromptAsync(ct);

        Assert.Contains(
            "Each Room you are in is a separate conversation, and this session holds exactly one of them.",
            appendedPrompt,
            StringComparison.Ordinal);
        Assert.DoesNotContain("This one session spans every Room you are in.", appendedPrompt, StringComparison.Ordinal);
    }

    /// <summary>A configured profile with <c>SessionPerRoom: false</c> keeps today's <c>systemPrompt.sharedSession</c> wording.</summary>
    [Fact]
    public async Task ProfileSessionPerRoomFalse_SelectsSharedText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.", Adapter: "claude");
        Dictionary<string, string?> additionalConfig = new(StringComparer.Ordinal)
        {
            ["Team:Acp:Adapters:0:Id"] = "claude",
            ["Team:Acp:Adapters:0:Command"] = "node",
            ["Team:Acp:Adapters:0:Args:0"] = "--mock-adapter-fixture",
            ["Team:Acp:Adapters:0:UsesToolNamePrefix"] = "true",
            ["Team:Acp:Adapters:0:SessionPerRoom"] = "false",
        };

        await using MockAdapterFixture fixture =
            await MockAdapterFixture.StartAsync(persona, additionalConfig, cancellationToken: ct);

        string appendedPrompt = await fixture.AppendedSystemPromptAsync(ct);

        Assert.Contains("This one session spans every Room you are in.", appendedPrompt, StringComparison.Ordinal);
    }
}
