namespace Agency.Huddle.Acp.Tests.Abstractions;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Agency.Huddle.Acp.Abstractions;
using Xunit;

public sealed class AgentSessionOptionsTests
{
    [Fact]
    public void Ctor_RelativeCwd_ThrowsArgumentException()
    {
        IPermissionHandler handler = new NeverCalledPermissionHandler();

        Assert.Throws<ArgumentException>(() => new AgentSessionOptions("relative/dir", handler));
    }

    [Fact]
    public void Ctor_NullHandler_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AgentSessionOptions(Path.GetTempPath(), null!));
    }

    [Fact]
    public void Ctor_RootedCwd_Succeeds()
    {
        IPermissionHandler handler = new NeverCalledPermissionHandler();
        string cwd = Path.GetTempPath();

        AgentSessionOptions options = new AgentSessionOptions(cwd, handler);

        Assert.Equal(cwd, options.Cwd);
        Assert.Same(handler, options.PermissionHandler);
    }

    [Fact]
    public void TwoArgCtor_SystemPromptIsNull()
    {
        IPermissionHandler handler = new NeverCalledPermissionHandler();

        AgentSessionOptions options = new AgentSessionOptions(Path.GetTempPath(), handler);

        Assert.Null(options.SystemPrompt);
    }

    [Fact]
    public void ThreeArgCtor_SystemPromptIsSameInstance()
    {
        IPermissionHandler handler = new NeverCalledPermissionHandler();
        SystemPromptOptions systemPrompt = new SystemPromptOptions("You are the COO");

        AgentSessionOptions options = new AgentSessionOptions(Path.GetTempPath(), handler, systemPrompt);

        Assert.Same(systemPrompt, options.SystemPrompt);
    }

    /// <summary>With no effort supplied, <see cref="AgentSessionOptions.Effort"/> defaults to null.</summary>
    [Fact]
    public void Effort_DefaultsToNull()
    {
        IPermissionHandler handler = new NeverCalledPermissionHandler();

        AgentSessionOptions options = new AgentSessionOptions(Path.GetTempPath(), handler);

        Assert.Null(options.Effort);
    }

    /// <summary>A blank effort is normalised to null, mirroring how <see cref="AgentSessionOptions.Model"/> is normalised.</summary>
    [Fact]
    public void Effort_BlankIsNormalisedToNull()
    {
        IPermissionHandler handler = new NeverCalledPermissionHandler();

        AgentSessionOptions options = new AgentSessionOptions(Path.GetTempPath(), handler, effort: "   ");

        Assert.Null(options.Effort);
    }

    /// <summary>With no mode supplied, <see cref="AgentSessionOptions.Mode"/> defaults to null.</summary>
    [Fact]
    public void Mode_DefaultsToNull()
    {
        IPermissionHandler handler = new NeverCalledPermissionHandler();

        AgentSessionOptions options = new AgentSessionOptions(Path.GetTempPath(), handler);

        Assert.Null(options.Mode);
    }

    /// <summary>A blank mode is normalised to null, mirroring how <see cref="AgentSessionOptions.Effort"/> is normalised.</summary>
    [Fact]
    public void Mode_BlankIsNormalisedToNull()
    {
        IPermissionHandler handler = new NeverCalledPermissionHandler();

        AgentSessionOptions options = new AgentSessionOptions(Path.GetTempPath(), handler, mode: "   ");

        Assert.Null(options.Mode);
    }

    /// <summary>A supplied mode id is carried through unchanged.</summary>
    [Fact]
    public void Mode_Supplied_IsCarriedThrough()
    {
        IPermissionHandler handler = new NeverCalledPermissionHandler();

        AgentSessionOptions options = new AgentSessionOptions(Path.GetTempPath(), handler, mode: "plan");

        Assert.Equal("plan", options.Mode);
    }

    private sealed class NeverCalledPermissionHandler : IPermissionHandler
    {
        public Task<PermissionDecision> DecideAsync(PermissionRequestContext context, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("This handler should not be invoked by these tests.");
        }
    }
}
