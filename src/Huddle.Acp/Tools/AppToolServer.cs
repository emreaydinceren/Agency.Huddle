namespace Agency.Huddle.Acp.Tools;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Agency.Huddle.Acp.Abstractions;

/// <summary>
/// Hosts an MCP-over-HTTP server, backed by <see cref="IAppTool"/> implementations whose bodies
/// execute inside our own process. Speaks the minimal JSON-RPC subset an ACP agent needs:
/// <c>initialize</c>, <c>tools/list</c>, <c>tools/call</c>, plus notification handling.
/// </summary>
public sealed partial class AppToolServer(
    string name, IReadOnlyList<IAppTool> tools, ILoggerFactory loggerFactory, int port = 0, string? authToken = null)
    : IAsyncDisposable
{
    private readonly ILogger logger = loggerFactory.CreateLogger<AppToolServer>();

    private readonly Lock gate = new Lock();

    // Computed once from the constructor parameter and never logged or otherwise surfaced -
    // this is the only place the token's value is held.
    private readonly string? expectedAuthorizationHeaderValue =
        string.IsNullOrWhiteSpace(authToken) ? null : $"Bearer {authToken}";

    private WebApplication? app;

    private ToolServerEndpoint? endpoint;

    private bool started;

    private bool disposed;

    public ToolServerEndpoint Endpoint => this.endpoint ?? throw new InvalidOperationException("The server has not been started.");

    /// <summary>Gets the names of the tools this server exposes, for diagnostics.</summary>
    public IReadOnlyList<string> ToolNames => [.. tools.Select(tool => tool.Name)];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(tools);

        lock (this.gate)
        {
            if (this.started)
            {
                throw new InvalidOperationException("The server has already been started.");
            }

            this.started = true;
        }

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        // Port 0 lets the OS pick. A fixed port exists so the URL can be written into a
        // .mcp.json ahead of time, which is a different registration path from session/new.
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        builder.Logging.ClearProviders();

        WebApplication builtApp = builder.Build();
        builtApp.MapPost("/mcp", (Delegate)this.HandleRequestAsync);

        await builtApp.StartAsync(cancellationToken).ConfigureAwait(false);

        IServerAddressesFeature addressesFeature = builtApp.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("The server did not report its bound addresses.");
        string boundAddress = addressesFeature.Addresses.First();
        Uri uri = new Uri(boundAddress.TrimEnd('/') + "/mcp");

        IReadOnlyDictionary<string, string>? headers = this.expectedAuthorizationHeaderValue is null
            ? null
            : new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Authorization"] = this.expectedAuthorizationHeaderValue,
            };

        this.app = builtApp;
        this.endpoint = new ToolServerEndpoint(name, uri, headers);
        AppToolServer.LogStarted(this.logger, uri);
    }

    public async ValueTask DisposeAsync()
    {
        lock (this.gate)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
        }

        WebApplication? activeApp = this.app;
        if (activeApp is not null)
        {
            await activeApp.DisposeAsync().ConfigureAwait(false);
        }
    }

    private JsonObject CreateInitializeResult()
    {
        return new JsonObject
        {
            ["protocolVersion"] = "2024-11-05",
            ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
            ["serverInfo"] = new JsonObject { ["name"] = name, ["version"] = "0.1.0" },
        };
    }

    private JsonObject CreateToolsListResult()
    {
        JsonArray toolEntries = [];
        foreach (IAppTool tool in tools)
        {
            toolEntries.Add(new JsonObject
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["inputSchema"] = tool.InputSchema.DeepClone(),
            });
        }

        return new JsonObject { ["tools"] = toolEntries };
    }

    private async Task<JsonObject> CreateToolsCallResultAsync(JsonObject? parameters, CancellationToken cancellationToken)
    {
        string? requestedName = (string?)parameters?["name"];
        JsonObject arguments = parameters?["arguments"] as JsonObject ?? [];

        IAppTool? tool = tools.FirstOrDefault(candidate => string.Equals(candidate.Name, requestedName, StringComparison.Ordinal));
        if (tool is null)
        {
            return AppToolServer.CreateErrorResult($"Unknown tool '{requestedName}'.");
        }

        try
        {
            string text = await tool.InvokeAsync(arguments, cancellationToken).ConfigureAwait(false);
            return new JsonObject
            {
                ["content"] = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = text } },
                ["isError"] = false,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AppToolServer.LogToolInvocationFailed(this.logger, tool.Name, ex);
            return AppToolServer.CreateErrorResult(ex.Message);
        }
    }

    private static JsonObject CreateErrorResult(string message)
    {
        return new JsonObject
        {
            ["content"] = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = message } },
            ["isError"] = true,
        };
    }

    private async Task HandleRequestAsync(HttpContext context)
    {
        if (this.expectedAuthorizationHeaderValue is not null)
        {
            string? providedAuthorizationHeaderValue = context.Request.Headers.Authorization.ToString();
            if (string.IsNullOrEmpty(providedAuthorizationHeaderValue)
                || !string.Equals(providedAuthorizationHeaderValue, this.expectedAuthorizationHeaderValue, StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;

                // Spec §6.9: bare challenge, deliberately with no resource_metadata. MCP's
                // authorization spec keys OAuth discovery off a 401 carrying WWW-Authenticate
                // *with* resource_metadata; Agency's MCP client leaves HttpClientTransportOptions.OAuth
                // unset, so a parameterised challenge risks sending a future SDK version down a
                // discovery path neither side has designed. A bare one is inert to the client and
                // makes a raw wire trace self-explanatory.
                context.Response.Headers.WWWAuthenticate = "Bearer";
                return;
            }
        }

        string body;
        using (StreamReader reader = new StreamReader(context.Request.Body))
        {
            body = await reader.ReadToEndAsync(context.RequestAborted).ConfigureAwait(false);
        }

        JsonObject? request = JsonNode.Parse(body) as JsonObject;
        JsonNode? idNode = request?["id"];
        if (idNode is null)
        {
            context.Response.StatusCode = StatusCodes.Status202Accepted;
            return;
        }

        string? method = (string?)request?["method"];

        // Logged so a silent "the agent never saw my tool" can be told apart from "the agent
        // saw it and chose not to call it": no request here at all means the harness never
        // connected, which is a configuration fault rather than a model decision.
        AppToolServer.LogRequest(this.logger, method ?? "(none)");
        JsonObject responseBody = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = idNode.DeepClone(),
        };

        switch (method)
        {
            case "initialize":
            responseBody["result"] = this.CreateInitializeResult();
            break;

            case "tools/list":
            responseBody["result"] = this.CreateToolsListResult();
            break;

            case "tools/call":
            responseBody["result"] = await this.CreateToolsCallResultAsync(
                request?["params"] as JsonObject, context.RequestAborted).ConfigureAwait(false);
            break;

            default:
            responseBody["error"] = new JsonObject { ["code"] = -32601, ["message"] = $"Method not found: {method}" };
            break;
        }

        context.Response.ContentType = "application/json";
        string payload = responseBody.ToJsonString();
        AppToolServer.LogResponse(this.logger, method ?? "(none)", payload);
        await context.Response.WriteAsync(payload, context.RequestAborted).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Trace, Message = "App tool server replied to {Method}: {Payload}")]
    private static partial void LogResponse(ILogger logger, string method, string payload);

    [LoggerMessage(Level = LogLevel.Debug, Message = "App tool server received {Method}.")]
    private static partial void LogRequest(ILogger logger, string method);

    [LoggerMessage(Level = LogLevel.Debug, Message = "App tool server listening at {Uri}.")]
    private static partial void LogStarted(ILogger logger, Uri uri);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tool '{ToolName}' invocation failed.")]
    private static partial void LogToolInvocationFailed(ILogger logger, string toolName, Exception exception);
}