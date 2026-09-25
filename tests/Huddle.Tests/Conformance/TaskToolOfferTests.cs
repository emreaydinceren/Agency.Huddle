using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// Spec §11.1 (Task 10.8, corrections-B4 D10 item 9): whether the six Task tools reach a real
/// composed system prompt at all, driven the same way <see cref="FileChangesToolOfferTests"/> drives
/// its own pair - a real <see cref="Agency.Huddle.App.Acp.DotAcpAgentHostFactory"/> against
/// <see cref="MockAdapterFixture"/>, inspecting the appended system prompt's own <c>tools</c> list.
/// </summary>
public sealed class TaskToolOfferTests
{
    /// <summary>With <c>Team:Tasks:Enabled</c> at its default (true), all six Task tools are offered to a Persona with no Skills, by their prefixed model-facing names.</summary>
    [Fact]
    public async Task TasksEnabled_OffersAllSixTaskTools()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.");

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(persona, cancellationToken: ct);

        string toolNames = await TaskToolOfferTests.GetToolNamesTextAsync(fixture, ct);

        // contains-ok: toolNames is the whole composed system prompt, which also lists every
        // non-Task tool; this test's intent is only that these six names are among them.
        Assert.Contains("mcp__team__create_task", toolNames, StringComparison.Ordinal);
        Assert.Contains("mcp__team__get_task", toolNames, StringComparison.Ordinal);
        Assert.Contains("mcp__team__list_tasks", toolNames, StringComparison.Ordinal);
        Assert.Contains("mcp__team__update_task", toolNames, StringComparison.Ordinal);
        Assert.Contains("mcp__team__close_task", toolNames, StringComparison.Ordinal);
        Assert.Contains("mcp__team__reopen_task", toolNames, StringComparison.Ordinal);
    }

    /// <summary>With <c>Team:Tasks:Enabled</c> false, none of the six Task tools are offered.</summary>
    [Fact]
    public async Task TasksDisabled_OffersNone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.");
        Dictionary<string, string?> additionalConfig = new(StringComparer.Ordinal)
        {
            ["Team:Tasks:Enabled"] = "false",
        };

        await using MockAdapterFixture fixture =
            await MockAdapterFixture.StartAsync(persona, additionalConfig, cancellationToken: ct);

        string toolNames = await TaskToolOfferTests.GetToolNamesTextAsync(fixture, ct);

        Assert.DoesNotContain("create_task", toolNames, StringComparison.Ordinal);
        Assert.DoesNotContain("get_task", toolNames, StringComparison.Ordinal);
        Assert.DoesNotContain("list_tasks", toolNames, StringComparison.Ordinal);
        Assert.DoesNotContain("update_task", toolNames, StringComparison.Ordinal);
        Assert.DoesNotContain("close_task", toolNames, StringComparison.Ordinal);
        Assert.DoesNotContain("reopen_task", toolNames, StringComparison.Ordinal);
    }

    /// <summary>Waits for <c>session/new</c> and returns the composed system prompt's own tool-list text, where every offered tool's prefixed name is named (matching <see cref="SystemPromptComposer"/>'s <c>systemPrompt.tools</c> rendering).</summary>
    private static async Task<string> GetToolNamesTextAsync(MockAdapterFixture fixture, CancellationToken cancellationToken)
    {
        JsonObject sessionNewRequest = await fixture.Agent.WaitForAsync("session/new", TimeSpan.FromSeconds(5));
        cancellationToken.ThrowIfCancellationRequested();

        JsonObject? parameters = sessionNewRequest["params"] as JsonObject;
        JsonObject? meta = parameters?["_meta"] as JsonObject;
        JsonObject? systemPrompt = meta?["systemPrompt"] as JsonObject;
        string? appendedPrompt = (string?)systemPrompt?["append"];

        Assert.NotNull(appendedPrompt);
        return appendedPrompt;
    }
}
