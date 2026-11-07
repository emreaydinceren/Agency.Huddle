using System.Text.RegularExpressions;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// Pins, whole and in order, the tools a real composed system prompt names. A Feature Module adds its tools at a fixed point in
/// the factory's list, and that order reaches the prompt, so moving a tool into a module must leave these lists alone.
/// </summary>
public sealed partial class ToolOfferOrderTests
{
    private static readonly string[] EveryAgentTools =
    [
        "mcp__team__get_help",
        "mcp__team__list_agents",
        "mcp__team__search",
        "mcp__team__create_room",
        "mcp__team__invite_agent",
        "mcp__team__post_message",
        "mcp__team__follow_room",
        "mcp__team__unfollow_room",
        "mcp__team__ask_human",
        "mcp__team__memory",
        "mcp__team__create_task",
        "mcp__team__get_task",
        "mcp__team__list_tasks",
        "mcp__team__update_task",
        "mcp__team__close_task",
        "mcp__team__reopen_task",
        "mcp__team__add_evidence",
        "mcp__team__jobs",
    ];

    /// <summary>An Adapter that reads files is offered every tool, with the File Changes tools and <c>log_change</c> after the chat tools.</summary>
    [Fact]
    public async Task ReadsFilesTrue_NamesEveryToolInOrder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.");

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(persona, cancellationToken: ct);

        Assert.Equal(
            [.. EveryAgentTools, "mcp__team__watch_folder", "mcp__team__unwatch_folder", "mcp__team__log_change"],
            NamedTools(await fixture.AppendedSystemPromptAsync(ct)));
    }

    /// <summary>An Adapter that cannot read files gets the same list without the File Changes tools and <c>log_change</c>.</summary>
    [Fact]
    public async Task ReadsFilesFalse_NamesEveryToolInOrder()
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

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(persona, additionalConfig, cancellationToken: ct);

        Assert.Equal(EveryAgentTools, NamedTools(await fixture.AppendedSystemPromptAsync(ct)));
    }

    /// <summary>The prefixed tool names in a system prompt, each once, in the order they first appear.</summary>
    /// <param name="prompt">The appended system prompt.</param>
    /// <returns>The tool names.</returns>
    private static List<string> NamedTools(string prompt)
    {
        return [.. PrefixedToolName().Matches(prompt).Select(static match => match.Value).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>Matches one tool name with its Adapter's prefix.</summary>
    /// <returns>The regular expression.</returns>
    [GeneratedRegex(@"mcp__team__\w+", RegexOptions.CultureInvariant)]
    private static partial Regex PrefixedToolName();
}
