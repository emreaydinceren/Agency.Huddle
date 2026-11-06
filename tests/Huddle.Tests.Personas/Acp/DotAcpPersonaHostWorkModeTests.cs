using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.Tests.Elicitation;
using Agency.Huddle.Tests.Factories;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Pins the two decisions <see cref="DotAcpPersonaHost"/> makes about a Work Mode when it builds a
/// session's options (ADR-0033): which mode is actually sent, and whether the permission handler is
/// wrapped by the plan guard.
/// </summary>
public sealed class DotAcpPersonaHostWorkModeTests
{
    /// <summary>On an Adapter that does not isolate the Human's settings, no Work Mode passes through as none, so nothing is sent.</summary>
    [Fact]
    public void EffectiveMode_NoWorkModeNotIsolated_IsNull()
    {
        string? mode = Policy().EffectiveMode("nova", null, false, NullLogger.Instance);

        Assert.Null(mode);
    }

    /// <summary>On an isolating Adapter, no Work Mode means the mode Huddle chooses, with no warning: nothing is wrong (issue #414).</summary>
    [Fact]
    public void EffectiveMode_NoWorkModeIsolated_IsTheModeHuddleChooses()
    {
        RecordingLogger logger = new();

        string? mode = Policy().EffectiveMode("nova", null, true, logger);

        Assert.Equal("default", mode);
        Assert.Empty(logger.Levels);
    }

    /// <summary>An operator who hides Manual is obeyed: the hidden list applies to Huddle's own choice as well, and the session keeps the Adapter's mode.</summary>
    [Fact]
    public void EffectiveMode_ManualHiddenIsolated_IsNull()
    {
        TeamOptions teamOptions = new();
        teamOptions.Acp.HiddenModes = ["default"];
        WorkModePolicy policy = new(Options.Create(teamOptions));

        string? mode = policy.EffectiveMode("nova", null, true, NullLogger.Instance);

        Assert.Null(mode);
    }

    /// <summary>An offered mode passes through unchanged.</summary>
    [Theory]
    [InlineData("default", false)]
    [InlineData("acceptEdits", false)]
    [InlineData("default", true)]
    [InlineData("acceptEdits", true)]
    public void EffectiveMode_OfferedMode_IsReturned(string workMode, bool isolated)
    {
        string? mode = Policy().EffectiveMode("nova", workMode, isolated, NullLogger.Instance);

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

        string? mode = Policy().EffectiveMode("nova", workMode, false, logger);

        Assert.Null(mode);
        Assert.Equal([LogLevel.Warning], logger.Levels);
    }

    /// <summary>On an isolating Adapter, a dropped hidden mode falls to the mode Huddle chooses, still with the warning (issue #414).</summary>
    [Theory]
    [InlineData("bypassPermissions")]
    [InlineData("auto")]
    [InlineData("plan")]
    public void EffectiveMode_HiddenModeIsolated_IsTheModeHuddleChoosesWithAWarning(string workMode)
    {
        RecordingLogger logger = new();

        string? mode = Policy().EffectiveMode("nova", workMode, true, logger);

        Assert.Equal("default", mode);
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
        return new WorkModePolicy(TestOptions.Create());
    }
}
