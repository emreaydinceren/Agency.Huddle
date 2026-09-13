namespace Agency.Huddle.Tests.Acp.Tools;

using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.Tests.Acp.Fakes;

public sealed class ListAgentsToolTests
{
    [Fact]
    public async Task ListAgents_IncludesRegisteredAgentsAndPersonas()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);
        var gateway = new FakeAgentGateway();
        gateway.SetOnline(echo.Id);
        using var personaStore = new PersonaStore(dir.Options(), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), NullLogger<PersonaStore>.Instance);
        personaStore.Add(new PersonaIdentity("coo", "coo", "coo", []), "You are the Chief of Staff.");
        var tool = new ListAgentsTool(directory, gateway, personaStore, new FakeHookSource());

        var result = await tool.InvokeAsync(new JsonObject(), ct);

        Assert.Contains("echo (online)", result, StringComparison.Ordinal);
        Assert.Contains("coo", result, StringComparison.Ordinal);
    }

    /// <summary>A Persona with frontmatter has its job description shown under both the Agents and Personas sections.</summary>
    [Fact]
    public async Task ListAgents_IncludesJobDescriptionComposedFromPersonaFrontmatter()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var coo = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(coo);
        var gateway = new FakeAgentGateway();
        gateway.SetOnline(coo.Id);

        // "role" is not one of the four structural identity fields PersonaStore.Add composes, so
        // this file - proving ListAgentsTool surfaces an OTHER frontmatter field through
        // PersonaFrontmatter.ComposeJobDescription - is written directly, as a hand-authored file
        // would be, rather than through Add.
        var teamsDir = Path.Combine(dir.Path, "Teams");
        Directory.CreateDirectory(teamsDir);
        await File.WriteAllTextAsync(
            Path.Combine(teamsDir, "coo.md"),
            "---\nName: coo\nTitle: coo\nAlias: coo\nrole: 'Router, triage, and cross-workstation continuity'\n---\nYou are the Chief of Staff.",
            ct);

        using var personaStore = new PersonaStore(dir.Options(), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), NullLogger<PersonaStore>.Instance);
        var tool = new ListAgentsTool(directory, gateway, personaStore, new FakeHookSource());

        var result = await tool.InvokeAsync(new JsonObject(), ct);

        Assert.Contains("Role: Router, triage, and cross-workstation continuity", result, StringComparison.Ordinal);
    }

    /// <summary>A Persona with no frontmatter (or an Agent with no matching Persona) shows no description lines.</summary>
    [Fact]
    public async Task ListAgents_PersonaWithNoFrontmatter_ShowsNoDescriptionLines()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);
        var gateway = new FakeAgentGateway();
        gateway.SetOnline(echo.Id);
        using var personaStore = new PersonaStore(dir.Options(), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), NullLogger<PersonaStore>.Instance);
        var tool = new ListAgentsTool(directory, gateway, personaStore, new FakeHookSource());

        var result = await tool.InvokeAsync(new JsonObject(), ct);

        Assert.Equal("Agents:\n- echo (online)\n\nPersonas: none", result);
    }

    /// <summary>Minimal valid Persona frontmatter (Name, Title and Alias all <paramref name="name"/>) wrapped around <paramref name="body"/> - identity is front-matter driven from this phase on.</summary>
    private static string PersonaText(string name, string body) => $"---\nName: {name}\nTitle: {name}\nAlias: {name}\n---\n{body}";
}