using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

    /// <summary>The real factory reaches the module's Work Dir step: a session offered <c>log_change</c> has the Stop hook in its settings before the Adapter opens.</summary>
    [Fact]
    public async Task Default_InstallsTheStopHook()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.");

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(persona, cancellationToken: ct);
        _ = await fixture.SessionNewAsync(ct);

        Assert.Contains("\"Stop\"", await WorkDirSettingsAsync(fixture, persona, ct), StringComparison.Ordinal);
    }

    /// <summary>A session that is not offered <c>log_change</c> gets no Stop hook.</summary>
    [Fact]
    public async Task Disabled_InstallsNoStopHook()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.");
        Dictionary<string, string?> additionalConfig = new(StringComparer.Ordinal) { ["Team:DocHistory:Enabled"] = "false" };

        await using MockAdapterFixture fixture =
            await MockAdapterFixture.StartAsync(persona, additionalConfig, cancellationToken: ct);
        _ = await fixture.SessionNewAsync(ct);

        Assert.DoesNotContain("\"Stop\"", await WorkDirSettingsAsync(fixture, persona, ct), StringComparison.Ordinal);
    }

    /// <summary>Reads the settings file the Adapter reads from the Persona's Work Dir.</summary>
    /// <param name="fixture">The started fixture.</param>
    /// <param name="persona">The Persona.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The file's text, or an empty string when nothing wrote it.</returns>
    private static async Task<string> WorkDirSettingsAsync(MockAdapterFixture fixture, Persona persona, CancellationToken cancellationToken)
    {
        IOptions<TeamOptions> options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        string path = Path.Combine(new AgentPaths(options).WorkDir(persona.Name), ".claude", WorkDirHookSettings.SettingsFile);

        return File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken) : string.Empty;
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
