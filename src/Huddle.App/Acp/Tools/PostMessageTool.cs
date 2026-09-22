namespace Agency.Huddle.App.Acp.Tools;

using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Services;

/// <summary>Posts a Message into a Room, as the calling Agent.</summary>
internal sealed class PostMessageTool(ChatService chat, string callerAgentId, IPromptSource prompts) : IAppTool
{
    private static readonly IReadOnlyDictionary<string, string> NoValues = new Dictionary<string, string>();

    public string Name => "post_message";

    public string Description => prompts.Render("tool.postMessage.description", NoValues);

    public JsonObject InputSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["roomId"] = new JsonObject { ["type"] = "string" },
            ["text"] = new JsonObject { ["type"] = "string" },
        },
        ["required"] = new JsonArray { "roomId", "text" },
    };

    public async Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var roomId = (string?)arguments["roomId"];
        var text = (string?)arguments["text"];
        if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(text))
        {
            return "Both 'roomId' and 'text' are required arguments.";
        }

        try
        {
            await chat.PostAsync(roomId, callerAgentId, text, ct: cancellationToken);
            return $"Posted to room '{roomId}'.";
        }
        catch (ChatException ex)
        {
            return $"Could not post the message: {ex.Message}";
        }
    }
}