namespace Agency.Huddle.Acp.Tests.Tools;

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Agency.Huddle.Acp.Tests.Fakes;
using Agency.Huddle.Acp.Tools;
using Agency.Huddle.Console.Tools;
using Xunit;

/// <summary>
/// Exercises <see cref="AppToolServer"/> over real loopback HTTP with a plain <see cref="HttpClient"/> -
/// this is our own server, so System.Text.Json (not the Newtonsoft wire used by the dotacp adapter) is fine.
/// </summary>
public sealed class AppToolServerTests
{
    [Fact(Timeout = 10000)]
    public async Task Initialize_ReturnsProtocolVersionAndServerInfoName()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ChatRoomRegistry registry = new ChatRoomRegistry();
        AppToolServer server = new AppToolServer(
            "team", [new ListChatRoomsTool(registry), new CreateChatRoomTool(registry)], new ListLoggerFactory());

        try
        {
            await server.StartAsync(cancellationToken);
            using HttpClient client = new HttpClient();

            JsonObject response = await AppToolServerTests.PostAsync(
                client,
                server.Endpoint.Uri,
                AppToolServerTests.CreateRequest(1, "initialize", new JsonObject()),
                cancellationToken);

            JsonObject result = Assert.IsType<JsonObject>(response["result"]);
            Assert.NotNull(result["protocolVersion"]);
            JsonObject serverInfo = Assert.IsType<JsonObject>(result["serverInfo"]);
            Assert.Equal((string?)"team", (string?)serverInfo["name"]);
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task ToolsList_ListsBothChatRoomToolsWithTheirSchemas()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ChatRoomRegistry registry = new ChatRoomRegistry();
        AppToolServer server = new AppToolServer(
            "team", [new ListChatRoomsTool(registry), new CreateChatRoomTool(registry)], new ListLoggerFactory());

        try
        {
            await server.StartAsync(cancellationToken);
            using HttpClient client = new HttpClient();

            JsonObject response = await AppToolServerTests.PostAsync(
                client,
                server.Endpoint.Uri,
                AppToolServerTests.CreateRequest(1, "tools/list", new JsonObject()),
                cancellationToken);

            JsonObject result = Assert.IsType<JsonObject>(response["result"]);
            JsonArray tools = Assert.IsType<JsonArray>(result["tools"]);
            Assert.Equal(2, tools.Count);

            JsonObject listTool = AppToolServerTests.FindToolByName(tools, "list_chatrooms");
            Assert.Equal(
                "Lists the chat rooms that exist in the Team application.", (string?)listTool["description"]);
            JsonObject listSchema = Assert.IsType<JsonObject>(listTool["inputSchema"]);
            Assert.Equal("object", (string?)listSchema["type"]);

            JsonObject createTool = AppToolServerTests.FindToolByName(tools, "create_chatroom");
            Assert.Equal(
                "Creates a new chat room in the Team application so agents can collaborate.",
                (string?)createTool["description"]);
            JsonObject createSchema = Assert.IsType<JsonObject>(createTool["inputSchema"]);
            JsonObject createProperties = Assert.IsType<JsonObject>(createSchema["properties"]);
            Assert.True(createProperties.ContainsKey("name"));
            JsonArray createRequired = Assert.IsType<JsonArray>(createSchema["required"]);
            Assert.Contains(createRequired, node => (string?)node == "name");
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task ToolsCall_ListChatrooms_ReturnsTextContainingBananas()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ChatRoomRegistry registry = new ChatRoomRegistry();
        AppToolServer server = new AppToolServer(
            "team", [new ListChatRoomsTool(registry), new CreateChatRoomTool(registry)], new ListLoggerFactory());

        try
        {
            await server.StartAsync(cancellationToken);
            using HttpClient client = new HttpClient();

            JsonObject parameters = new JsonObject
            {
                ["name"] = "list_chatrooms",
                ["arguments"] = new JsonObject(),
            };
            JsonObject response = await AppToolServerTests.PostAsync(
                client,
                server.Endpoint.Uri,
                AppToolServerTests.CreateRequest(1, "tools/call", parameters),
                cancellationToken);

            JsonObject result = Assert.IsType<JsonObject>(response["result"]);
            Assert.Equal((bool?)false, (bool?)result["isError"]);
            JsonArray content = Assert.IsType<JsonArray>(result["content"]);
            JsonObject textBlock = Assert.Single(content) as JsonObject
                ?? throw new InvalidOperationException("Expected the single content entry to be a JSON object.");
            Assert.Contains("bananas", (string?)textBlock["text"], StringComparison.Ordinal);
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task ToolsCall_CreateChatroom_ReturnsIsErrorFalseAndRegistryGainsTheRoom()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ChatRoomRegistry registry = new ChatRoomRegistry();
        AppToolServer server = new AppToolServer(
            "team", [new ListChatRoomsTool(registry), new CreateChatRoomTool(registry)], new ListLoggerFactory());

        try
        {
            await server.StartAsync(cancellationToken);
            using HttpClient client = new HttpClient();

            JsonObject arguments = new JsonObject { ["name"] = "q4-planning" };
            JsonObject parameters = new JsonObject
            {
                ["name"] = "create_chatroom",
                ["arguments"] = arguments,
            };
            JsonObject response = await AppToolServerTests.PostAsync(
                client,
                server.Endpoint.Uri,
                AppToolServerTests.CreateRequest(1, "tools/call", parameters),
                cancellationToken);

            JsonObject result = Assert.IsType<JsonObject>(response["result"]);
            Assert.Equal((bool?)false, (bool?)result["isError"]);

            // The actual proof that the tool call executed OUR code, not merely that the server
            // replied with a plausible-sounding string: the registry instance itself gained the room.
            Assert.Contains("q4-planning", registry.List(), StringComparer.Ordinal);
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task ToolsCall_CreateChatroom_DuplicateName_ReturnsIsErrorAndRegistryUnchanged()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ChatRoomRegistry registry = new ChatRoomRegistry();
        AppToolServer server = new AppToolServer(
            "team", [new ListChatRoomsTool(registry), new CreateChatRoomTool(registry)], new ListLoggerFactory());

        try
        {
            await server.StartAsync(cancellationToken);
            using HttpClient client = new HttpClient();

            JsonObject parameters = new JsonObject
            {
                ["name"] = "create_chatroom",
                ["arguments"] = new JsonObject { ["name"] = "bananas" },
            };
            JsonObject response = await AppToolServerTests.PostAsync(
                client,
                server.Endpoint.Uri,
                AppToolServerTests.CreateRequest(1, "tools/call", parameters),
                cancellationToken);

            JsonObject result = Assert.IsType<JsonObject>(response["result"]);
            Assert.Equal((bool?)true, (bool?)result["isError"]);
            Assert.Single(registry.List());
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task ToolsCall_CreateChatroom_MissingName_ReturnsIsError()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ChatRoomRegistry registry = new ChatRoomRegistry();
        AppToolServer server = new AppToolServer(
            "team", [new ListChatRoomsTool(registry), new CreateChatRoomTool(registry)], new ListLoggerFactory());

        try
        {
            await server.StartAsync(cancellationToken);
            using HttpClient client = new HttpClient();

            JsonObject parameters = new JsonObject
            {
                ["name"] = "create_chatroom",
                ["arguments"] = new JsonObject(),
            };
            JsonObject response = await AppToolServerTests.PostAsync(
                client,
                server.Endpoint.Uri,
                AppToolServerTests.CreateRequest(1, "tools/call", parameters),
                cancellationToken);

            JsonObject result = Assert.IsType<JsonObject>(response["result"]);
            Assert.Equal((bool?)true, (bool?)result["isError"]);
            Assert.Single(registry.List());
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task Notification_WithoutId_GetsEmpty202AndNoResultBody()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ChatRoomRegistry registry = new ChatRoomRegistry();
        AppToolServer server = new AppToolServer(
            "team", [new ListChatRoomsTool(registry), new CreateChatRoomTool(registry)], new ListLoggerFactory());

        try
        {
            await server.StartAsync(cancellationToken);
            using HttpClient client = new HttpClient();

            JsonObject notification = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = "notifications/initialized",
            };

            using HttpResponseMessage httpResponse = await client.PostAsync(
                server.Endpoint.Uri,
                new StringContent(notification.ToJsonString(), Encoding.UTF8, "application/json"),
                cancellationToken);
            string body = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

            Assert.Equal(HttpStatusCode.Accepted, httpResponse.StatusCode);
            Assert.Empty(body);
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task ToolsList_WithTokenConfigured_NoAuthorizationHeader_Returns401()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ChatRoomRegistry registry = new ChatRoomRegistry();
        AppToolServer server = new AppToolServer(
            "team", [new ListChatRoomsTool(registry), new CreateChatRoomTool(registry)], new ListLoggerFactory(),
            authToken: "correct-token");

        try
        {
            await server.StartAsync(cancellationToken);
            using HttpClient client = new HttpClient();

            using HttpResponseMessage httpResponse = await client.PostAsync(
                server.Endpoint.Uri,
                new StringContent(
                    AppToolServerTests.CreateRequest(1, "tools/list", new JsonObject()).ToJsonString(),
                    Encoding.UTF8,
                    "application/json"),
                cancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, httpResponse.StatusCode);
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task ToolsList_WithTokenConfigured_WrongToken_Returns401()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ChatRoomRegistry registry = new ChatRoomRegistry();
        AppToolServer server = new AppToolServer(
            "team", [new ListChatRoomsTool(registry), new CreateChatRoomTool(registry)], new ListLoggerFactory(),
            authToken: "correct-token");

        try
        {
            await server.StartAsync(cancellationToken);
            using HttpClient client = new HttpClient();
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, server.Endpoint.Uri)
            {
                Content = new StringContent(
                    AppToolServerTests.CreateRequest(1, "tools/list", new JsonObject()).ToJsonString(),
                    Encoding.UTF8,
                    "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "wrong-token");

            using HttpResponseMessage httpResponse = await client.SendAsync(request, cancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, httpResponse.StatusCode);
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task ToolsList_WithTokenConfigured_CorrectToken_SucceedsAndListsBothTools()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ChatRoomRegistry registry = new ChatRoomRegistry();
        AppToolServer server = new AppToolServer(
            "team", [new ListChatRoomsTool(registry), new CreateChatRoomTool(registry)], new ListLoggerFactory(),
            authToken: "correct-token");

        try
        {
            await server.StartAsync(cancellationToken);
            using HttpClient client = new HttpClient();
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, server.Endpoint.Uri)
            {
                Content = new StringContent(
                    AppToolServerTests.CreateRequest(1, "tools/list", new JsonObject()).ToJsonString(),
                    Encoding.UTF8,
                    "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "correct-token");

            using HttpResponseMessage httpResponse = await client.SendAsync(request, cancellationToken);
            string body = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            JsonObject response = (JsonObject)(JsonNode.Parse(body)
                ?? throw new InvalidOperationException("Expected a JSON body in the response."));

            Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
            JsonObject result = Assert.IsType<JsonObject>(response["result"]);
            JsonArray tools = Assert.IsType<JsonArray>(result["tools"]);
            Assert.Equal(2, tools.Count);
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task Endpoint_WithTokenConfigured_HeadersContainsAuthorizationEntry()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ChatRoomRegistry registry = new ChatRoomRegistry();
        AppToolServer server = new AppToolServer(
            "team", [new ListChatRoomsTool(registry), new CreateChatRoomTool(registry)], new ListLoggerFactory(),
            authToken: "correct-token");

        try
        {
            await server.StartAsync(cancellationToken);

            Assert.True(server.Endpoint.Headers.TryGetValue("Authorization", out string? value));
            Assert.StartsWith("Bearer ", value, StringComparison.Ordinal);
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task ToolsList_NoTokenConfigured_UnauthenticatedRequestSucceedsAndEndpointHeadersIsEmpty()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ChatRoomRegistry registry = new ChatRoomRegistry();
        AppToolServer server = new AppToolServer(
            "team", [new ListChatRoomsTool(registry), new CreateChatRoomTool(registry)], new ListLoggerFactory());

        try
        {
            await server.StartAsync(cancellationToken);
            using HttpClient client = new HttpClient();

            JsonObject response = await AppToolServerTests.PostAsync(
                client,
                server.Endpoint.Uri,
                AppToolServerTests.CreateRequest(1, "tools/list", new JsonObject()),
                cancellationToken);

            JsonObject result = Assert.IsType<JsonObject>(response["result"]);
            JsonArray tools = Assert.IsType<JsonArray>(result["tools"]);
            Assert.Equal(2, tools.Count);

            // Pins that the feature stayed optional: with no token configured, behaviour is
            // exactly as it was before this change.
            Assert.Empty(server.Endpoint.Headers);
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact]
    public void Endpoint_BeforeStart_Throws()
    {
        ChatRoomRegistry registry = new ChatRoomRegistry();
        AppToolServer server = new AppToolServer(
            "team", [new ListChatRoomsTool(registry), new CreateChatRoomTool(registry)], new ListLoggerFactory());

        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = server.Endpoint;
        });
    }

    private static JsonObject FindToolByName(JsonArray tools, string name)
    {
        foreach (JsonNode? node in tools)
        {
            if (node is JsonObject candidate && (string?)candidate["name"] == name)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"Expected a tool named '{name}' in the tools/list response.");
    }

    private static JsonObject CreateRequest(int id, string method, JsonObject parameters)
    {
        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
            ["params"] = parameters,
        };
    }

    private static async Task<JsonObject> PostAsync(
        HttpClient client, Uri endpoint, JsonObject request, CancellationToken cancellationToken)
    {
        using HttpResponseMessage httpResponse = await client.PostAsync(
            endpoint,
            new StringContent(request.ToJsonString(), Encoding.UTF8, "application/json"),
            cancellationToken);
        string body = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
        JsonNode node = JsonNode.Parse(body)
            ?? throw new InvalidOperationException("Expected a JSON body in the response.");
        return (JsonObject)node;
    }
}
