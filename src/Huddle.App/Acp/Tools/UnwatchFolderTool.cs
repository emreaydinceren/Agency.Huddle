namespace Agency.Huddle.App.Acp.Tools;

using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Prompts;

/// <summary>Stops listing a folder's file changes — the counterpart of <see cref="WatchFolderTool"/>, per FC §6.9.</summary>
/// <remarks>
/// Follows <see cref="UnfollowRoomTool"/>'s pattern: delegates every result text to
/// <see cref="FileChangeTracker.Unsubscribe"/> verbatim, and never throws for an expected failure.
/// </remarks>
internal sealed class UnwatchFolderTool(FileChangeTracker tracker, string agentName, IPromptSource prompts) : IAppTool
{
    private static readonly IReadOnlyDictionary<string, string> NoValues = new Dictionary<string, string>();

    /// <inheritdoc />
    public string Name => "unwatch_folder";

    /// <inheritdoc />
    public string Description => prompts.Render("tool.unwatchFolder.description", NoValues);

    /// <inheritdoc />
    public JsonObject InputSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["folder"] = new JsonObject { ["type"] = "string" },
        },
        ["required"] = new JsonArray { "folder" },
    };

    /// <inheritdoc />
    public Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var folder = (string?)arguments["folder"];
        if (string.IsNullOrWhiteSpace(folder))
        {
            return Task.FromResult("'folder' is a required argument.");
        }

        return Task.FromResult(tracker.Unsubscribe(agentName, folder));
    }
}
