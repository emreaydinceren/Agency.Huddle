namespace Agency.Huddle.Console.Tools;

using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Agency.Huddle.Acp.Abstractions;

/// <summary>Lists the chat rooms currently known to <see cref="ChatRoomRegistry"/>.</summary>
internal sealed class ListChatRoomsTool(ChatRoomRegistry registry) : IAppTool
{
    public string Name => "list_chatrooms";

    public string Description => "Lists the chat rooms that exist in the Team application.";

    public JsonObject InputSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject(),
        ["required"] = new JsonArray(),
    };

    public Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        string text = "Chat rooms: " + string.Join(", ", registry.List());
        return Task.FromResult(text);
    }
}
