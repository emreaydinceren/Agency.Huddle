namespace Agency.Huddle.Console.Tools;

using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Agency.Huddle.Acp.Abstractions;

/// <summary>Creates a new chat room in <see cref="ChatRoomRegistry"/>.</summary>
internal sealed class CreateChatRoomTool(ChatRoomRegistry registry) : IAppTool
{
    public string Name => "create_chatroom";

    public string Description => "Creates a new chat room in the Team application so agents can collaborate.";

    public JsonObject InputSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["name"] = new JsonObject { ["type"] = "string" },
        },
        ["required"] = new JsonArray { "name" },
    };

    public Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        string? name = (string?)arguments["name"];
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("The 'name' argument is required and must not be whitespace.", nameof(arguments));
        }

        registry.Create(name);
        int roomCount = registry.List().Count;

        string text = $"Created chat room '{name}'. There are now {roomCount} chat rooms.";
        return Task.FromResult(text);
    }
}
