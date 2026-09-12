namespace Agency.Huddle.Acp.Tests.Abstractions;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Agency.Huddle.Acp.Abstractions;
using Xunit;

public sealed class AgentSessionOptionsToolServerTests
{
    [Fact]
    public void ThreeArgCtor_ToolServerIsNull()
    {
        IPermissionHandler handler = new NeverCalledPermissionHandler();
        SystemPromptOptions systemPrompt = new SystemPromptOptions("You are the COO");

        AgentSessionOptions options = new AgentSessionOptions(Path.GetTempPath(), handler, systemPrompt);

        Assert.Null(options.ToolServer);
    }

    [Fact]
    public void FourArgCtor_ToolServerIsSameInstance()
    {
        IPermissionHandler handler = new NeverCalledPermissionHandler();
        SystemPromptOptions systemPrompt = new SystemPromptOptions("You are the COO");
        ToolServerEndpoint toolServer = new ToolServerEndpoint("team", new Uri("http://127.0.0.1:5057/mcp"));

        AgentSessionOptions options = new AgentSessionOptions(Path.GetTempPath(), handler, systemPrompt, toolServer);

        Assert.Same(toolServer, options.ToolServer);
    }

    private sealed class NeverCalledPermissionHandler : IPermissionHandler
    {
        public Task<PermissionDecision> DecideAsync(PermissionRequestContext context, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("This handler should not be invoked by these tests.");
        }
    }
}
