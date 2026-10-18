using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Pins the two decisions <see cref="DotAcpPersonaHost"/> makes about a Work Mode when it builds a
/// session's options (ADR-0033): which mode is actually sent, and whether the permission handler is
/// wrapped by the plan guard.
/// </summary>
public sealed class DotAcpPersonaHostWorkModeTests
{
    /// <summary>No Work Mode passes through as none, so nothing is sent.</summary>
    [Fact]
    public void EffectiveMode_NoWorkMode_IsNull()
    {
        string? mode = Policy().EffectiveMode("nova", null, NullLogger.Instance);

        Assert.Null(mode);
    }

    /// <summary>An offered mode passes through unchanged.</summary>
    [Theory]
    [InlineData("default")]
    [InlineData("acceptEdits")]
    public void EffectiveMode_OfferedMode_IsReturned(string workMode)
    {
        string? mode = Policy().EffectiveMode("nova", workMode, NullLogger.Instance);

        Assert.Equal(workMode, mode);
    }

    /// <summary>A hidden mode is dropped and a warning logged, so a hand-written database row cannot select it.</summary>
    [Theory]
    [InlineData("bypassPermissions")]
    [InlineData("auto")]
    [InlineData("plan")]
    public void EffectiveMode_HiddenMode_IsDroppedWithAWarning(string workMode)
    {
        RecordingLogger logger = new();

        string? mode = Policy().EffectiveMode("nova", workMode, logger);

        Assert.Null(mode);
        Assert.Equal([LogLevel.Warning], logger.Levels);
    }

    /// <summary>A plan Persona's handler is wrapped by the guard, so the Adapter's request to leave plan mode is refused.</summary>
    [Fact]
    public void GuardPermissions_PlanMode_WrapsTheHandler()
    {
        IPermissionHandler inner = new AutoApprovePermissionHandler();

        IPermissionHandler guarded = DotAcpPersonaHost.GuardPermissions(inner, "plan", NullLoggerFactory.Instance);

        Assert.IsType<PlanModePermissionHandler>(guarded);
    }

    /// <summary>Every other Persona keeps exactly the handler it has today.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("default")]
    [InlineData("acceptEdits")]
    public void GuardPermissions_AnyOtherMode_ReturnsTheHandlerUnchanged(string? mode)
    {
        IPermissionHandler inner = new AutoApprovePermissionHandler();

        IPermissionHandler guarded = DotAcpPersonaHost.GuardPermissions(inner, mode, NullLoggerFactory.Instance);

        Assert.Same(inner, guarded);
    }

    private static WorkModePolicy Policy()
    {
        return new WorkModePolicy(Options.Create(new TeamOptions()));
    }

    /// <summary>Records the level of every entry it is given.</summary>
    private sealed class RecordingLogger : ILogger
    {
        public List<LogLevel> Levels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            this.Levels.Add(logLevel);
        }
    }
}
