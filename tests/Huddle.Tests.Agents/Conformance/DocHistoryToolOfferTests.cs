using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// DocHistory spec §6.3: whether <c>log_change</c> reaches a real composed system prompt, driven the way
/// <see cref="FileChangesToolOfferTests"/> drives its tool list.
/// </summary>
public sealed class DocHistoryToolOfferTests
{
    /// <summary>With History on and an Adapter that reads files, <c>log_change</c> is offered by its prefixed name.</summary>
    [Fact]
    public async Task Default_OffersLogChange()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.");

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(persona, cancellationToken: ct);

        Assert.Contains("mcp__team__log_change", await fixture.AppendedSystemPromptAsync(ct), StringComparison.Ordinal);
    }

    /// <summary>With <c>Team:DocHistory:Enabled</c> false the tool is not offered.</summary>
    [Fact]
    public async Task Disabled_OffersNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.");
        Dictionary<string, string?> additionalConfig = new(StringComparer.Ordinal) { ["Team:DocHistory:Enabled"] = "false" };

        await using MockAdapterFixture fixture =
            await MockAdapterFixture.StartAsync(persona, additionalConfig, cancellationToken: ct);

        Assert.DoesNotContain("log_change", await fixture.AppendedSystemPromptAsync(ct), StringComparison.Ordinal);
    }

    /// <summary>An Adapter that cannot read files is not asked to log changes to files.</summary>
    [Fact]
    public async Task ReadsFilesFalse_OffersNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.", Adapter: "agency");
        Dictionary<string, string?> additionalConfig = new(StringComparer.Ordinal)
        {
            ["Team:Acp:Adapters:0:Id"] = "agency",
            ["Team:Acp:Adapters:0:Command"] = "node",
            ["Team:Acp:Adapters:0:Args:0"] = "--mock-adapter-fixture",
            ["Team:Acp:Adapters:0:ReadsFiles"] = "false",
        };

        await using MockAdapterFixture fixture =
            await MockAdapterFixture.StartAsync(persona, additionalConfig, cancellationToken: ct);

        Assert.DoesNotContain("log_change", await fixture.AppendedSystemPromptAsync(ct), StringComparison.Ordinal);
    }
}
