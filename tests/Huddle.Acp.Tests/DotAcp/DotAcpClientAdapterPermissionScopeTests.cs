using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Tests.Fakes;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Agency.Huddle.Acp.Tests.DotAcp;

/// <summary>
/// Covers what a permission request carries to its handler for the connector approval prompt (Agent Connectors spec 8.4): the
/// tool name and MCP server from <c>_meta.claudeCode</c>, the session's scope, and the lifetimes that must end a handler
/// waiting for the Human (the host's disposal, the session's unregistering).
/// </summary>
public sealed class DotAcpClientAdapterPermissionScopeTests
{
    private const string SessionId = "sess-1";

    /// <summary>The Adapter's tool name and the MCP server name in <c>_meta.claudeCode</c> reach the handler's tool call.</summary>
    [Fact]
    public void MapPermission_ClaudeCodeMeta_CarriesToolNameAndServer()
    {
        dotacp.protocol.RequestPermissionRequest request = Request(new Dictionary<string, object>
        {
            ["claudeCode"] = new JObject
            {
                ["toolName"] = "mcp__claude_ai_Gmail__send_message",
                ["mcpServer"] = new JObject { ["name"] = "claude_ai_Gmail", ["source"] = "claudeai" },
            },
        });

        PermissionRequestContext context = SessionUpdateMapper.MapPermission(request);

        Assert.Equal("mcp__claude_ai_Gmail__send_message", context.ToolCall.ToolName);
        Assert.Equal("claude_ai_Gmail", context.ToolCall.McpServerName);
        Assert.Equal("claudeai", context.ToolCall.McpServerSource);
    }

    /// <summary>A request with no <c>_meta</c> maps to no name and no server, never a guess.</summary>
    [Fact]
    public void MapPermission_NoMeta_NamesAreNull()
    {
        PermissionRequestContext context = SessionUpdateMapper.MapPermission(Request(meta: null));

        Assert.Null(context.ToolCall.ToolName);
        Assert.Null(context.ToolCall.McpServerName);
    }

    /// <summary>The session's scope is handed to the handler in the request context.</summary>
    [Fact(Timeout = 10000)]
    public async Task RequestPermission_PassesTheSessionScope()
    {
        RecordingElicitationScope scope = new();
        (DotAcpClientAdapter adapter, CapturingHandler handler) = Create(scope);

        _ = await adapter.RequestPermissionAsync(Request(meta: null), TestContext.Current.CancellationToken);

        Assert.Same(scope, handler.Context?.Scope);
    }

    /// <summary>A handler waiting for the Human is cancelled when the host disposes the adapter, so it never blocks the shutdown.</summary>
    [Fact(Timeout = 10000)]
    public async Task RequestPermission_AdapterDispose_CancelsTheHandlerToken()
    {
        (DotAcpClientAdapter adapter, CapturingHandler handler) = Create(scope: null);
        handler.Block = true;
        Task<dotacp.protocol.RequestPermissionResponse> pending = adapter.RequestPermissionAsync(Request(meta: null), TestContext.Current.CancellationToken);
        CancellationToken token = await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        adapter.Dispose();
        dotacp.protocol.RequestPermissionResponse response = await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(token.IsCancellationRequested);
        Assert.IsType<dotacp.protocol.RequestPermissionOutcomeCancelled>(response.Outcome);
    }

    /// <summary>A handler waiting for the Human is cancelled when its session is unregistered.</summary>
    [Fact(Timeout = 10000)]
    public async Task RequestPermission_Unregister_CancelsTheHandlerToken()
    {
        (DotAcpClientAdapter adapter, CapturingHandler handler) = Create(scope: null);
        handler.Block = true;
        Task<dotacp.protocol.RequestPermissionResponse> pending = adapter.RequestPermissionAsync(Request(meta: null), TestContext.Current.CancellationToken);
        CancellationToken token = await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        _ = adapter.Unregister(SessionId);
        _ = await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(token.IsCancellationRequested);
    }

    /// <summary>A lost connection cancels a handler waiting for the Human.</summary>
    [Fact(Timeout = 10000)]
    public async Task RequestPermission_Disconnect_CancelsTheHandlerToken()
    {
        (DotAcpClientAdapter adapter, CapturingHandler handler) = Create(scope: null);
        handler.Block = true;
        Task<dotacp.protocol.RequestPermissionResponse> pending = adapter.RequestPermissionAsync(Request(meta: null), TestContext.Current.CancellationToken);
        CancellationToken token = await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        adapter.OnDisconnected(null!);
        _ = await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(token.IsCancellationRequested);
    }

    private static (DotAcpClientAdapter Adapter, CapturingHandler Handler) Create(IElicitationScope? scope)
    {
        DotAcpClientAdapter adapter = new(new ListLogger<DotAcpClientAdapter>(), new WireOrderLedger());
        CapturingHandler handler = new();
        adapter.Register(new FakeSessionSink(SessionId) { PermissionHandler = handler, ElicitationScope = scope });
        return (adapter, handler);
    }

    private static dotacp.protocol.RequestPermissionRequest Request(Dictionary<string, object>? meta)
    {
        return new dotacp.protocol.RequestPermissionRequest
        {
            SessionId = SessionId,
            ToolCall = new dotacp.protocol.ToolCallUpdate
            {
                ToolCallId = "call-1",
                Title = "Send a message",
                Kind = dotacp.protocol.ToolKind.Other,
                Status = dotacp.protocol.ToolCallStatus.Pending,
                Meta = meta,
            },
            Options =
            [
                new dotacp.protocol.PermissionOption { OptionId = "allow", Name = "Allow once", Kind = dotacp.protocol.PermissionOptionKind.AllowOnce },
            ],
        };
    }

    private sealed class CapturingHandler : IPermissionHandler
    {
        public TaskCompletionSource<CancellationToken> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public PermissionRequestContext? Context { get; private set; }

        public bool Block { get; set; }

        public async Task<PermissionDecision> DecideAsync(PermissionRequestContext context, CancellationToken cancellationToken)
        {
            this.Context = context;
            _ = this.Entered.TrySetResult(cancellationToken);
            if (this.Block)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return PermissionDecision.Cancelled;
        }
    }
}
