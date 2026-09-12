namespace Agency.Huddle.App.Acp.Tools;

using System.Text;
using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;

/// <summary>
/// Explains the chat application and lists every App Tool available to the calling Agent, so the
/// rest of the tool surface can be discovered at run time instead of being carried in every prompt.
/// </summary>
/// <remarks>
/// <para>
/// This is the progressive-discovery entry point. The system prompt names only this tool; this tool
/// names the others. That keeps the per-turn cost of the prompt flat as tools are added, and gives
/// one place — rather than two — to describe how a Room behaves.
/// </para>
/// <para>
/// Every tool is listed with its full <c>mcp__team__</c> prefix, for the same reason
/// <see cref="SystemPromptComposer"/> uses it: a model in deferred-tool mode looks a name up
/// verbatim and reports that no such tool exists when it is named loosely.
/// </para>
/// </remarks>
internal sealed class GetHelpTool : IAppTool
{
    private readonly IReadOnlyList<IAppTool> otherTools;

    /// <summary>Initialises a new instance of the <see cref="GetHelpTool"/> class.</summary>
    /// <param name="otherTools">
    /// Every other tool offered to the same session. Copied on the way in, so the caller's array
    /// cannot change what this tool reports. This tool is not in the list and adds itself.
    /// </param>
    public GetHelpTool(IReadOnlyList<IAppTool> otherTools)
    {
        ArgumentNullException.ThrowIfNull(otherTools);

        this.otherTools = [.. otherTools];
    }

    /// <inheritdoc />
    public string Name => "get_help";

    /// <inheritdoc />
    public string Description =>
        "Explains how the Team chat application works — Rooms, members, mentions, and who is expected to reply " +
        "when — and lists every tool you can call here with its exact name. Call this before using any other Team " +
        "tool, and whenever you are unsure how something in the chat works. It takes no arguments.";

    /// <inheritdoc />
    public JsonObject InputSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject(),
        ["required"] = new JsonArray(),
    };

    /// <inheritdoc />
    public Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        return Task.FromResult(this.BuildHelp());
    }

    /// <summary>Builds the help text: how the chat works, then the catalog of tools.</summary>
    /// <returns>The help text handed back to the model.</returns>
    private string BuildHelp()
    {
        var builder = new StringBuilder();

        builder.Append(
            """
            Team is a chat application. You are one Teammate in it, talking to a human and to other
            agents in Rooms. You are not working alone at a terminal.

            ROOMS
            A Room is a conversation with a fixed set of members. There is only one kind of Room, and
            how you behave in it follows from how many members it has:
              - two members  — a private conversation with the human. Answer every message.
              - three or more — a group. Answer only when you are @-mentioned.

            MESSAGES
            Every message you are given starts with the Room it came from, written as
            "[Room: <name> (id: <id>)]". That id is what the tools below mean by a room id.
            Messages marked "context only" are ones you were not addressed in; read them for
            background, do not answer them.

            MENTIONS
            Address another member by writing @ followed by their exact name. A name may contain
            spaces, so "@Chief of Staff" is one mention of one member. Write the name exactly as it
            was given to you, with no quotes around it.

            REPLYING
            Your reply to the message you were given is simply your answer text. Do not also post it
            with a tool — that would deliver it twice.

            TOOLS

            """);

        foreach (var tool in this.AllTools())
        {
            builder.Append("  mcp__team__").Append(tool.Name).Append('\n');
            builder.Append("      ").Append(tool.Description).Append('\n');
            builder.Append('\n');
        }

        builder.Append(
            """
            Never answer a question about who exists, or about Rooms, from a codebase or from memory.
            Call the tools: they are the only source of truth about this application.
            """);

        return builder.ToString();
    }

    /// <summary>Returns this tool followed by every other tool, in the order they are reported.</summary>
    /// <returns>The full tool catalog, this tool first.</returns>
    private IEnumerable<IAppTool> AllTools()
    {
        yield return this;

        foreach (var tool in this.otherTools)
        {
            yield return tool;
        }
    }
}
