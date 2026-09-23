namespace Agency.Huddle.App.Acp.Tools;

using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Prompts;

/// <summary>Starts listing a folder's file changes at the top of every later Turn, per FC §6.9.</summary>
/// <remarks>
/// Follows <see cref="FollowRoomTool"/>'s pattern: an <c>agentName</c> bound at construction, a
/// single string argument, and result texts written in code — here, all of them owned by
/// <see cref="FileChangeTracker.Subscribe"/>, which this tool delegates to verbatim. Never throws
/// for an expected failure (a blank argument, an entry that does not resolve): both are reported as
/// text, matching every other App Tool in this folder.
/// </remarks>
internal sealed class WatchFolderTool(FileChangeTracker tracker, string agentName, IPromptSource prompts) : IAppTool
{
    private static readonly IReadOnlyDictionary<string, string> NoValues = new Dictionary<string, string>();

    /// <inheritdoc />
    public string Name => "watch_folder";

    /// <inheritdoc />
    public string Description => prompts.Render("tool.watchFolder.description", NoValues);

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

        return Task.FromResult(tracker.Subscribe(agentName, folder));
    }
}
