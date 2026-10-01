using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Pins the plan guard (ADR-0033). In <c>plan</c> mode the Adapter asks permission to leave it with a
/// <see cref="ToolKind.SwitchMode"/> request, and the handler beneath prefers the option that allows it
/// once, which would take the Persona out of plan mode on its first request. The guard refuses that one
/// request, and only for a Persona whose Work Mode is <c>plan</c>.
/// </summary>
public sealed class PlanModePermissionHandlerTests
{
    private static readonly IReadOnlyList<PermissionOptionInfo> ExitPlanOptions =
    [
        new PermissionOptionInfo("elevate", "Yes, and use auto", PermissionOptionKind.AllowAlways),
        new PermissionOptionInfo("manual", "Yes, manually approve edits", PermissionOptionKind.AllowOnce),
        new PermissionOptionInfo("keep-planning", "No, keep planning", PermissionOptionKind.RejectOnce),
    ];

    /// <summary>A switch-mode request in plan mode is refused with the one-time refusal, and never reaches the handler beneath.</summary>
    [Fact]
    public async Task Plan_SwitchModeRequest_IsRefusedWithRejectOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        StubHandler inner = new();
        PlanModePermissionHandler handler = new(inner, "plan", NullLogger<PlanModePermissionHandler>.Instance);

        PermissionDecision decision = await handler.DecideAsync(Request(ToolKind.SwitchMode, ExitPlanOptions), ct);

        SelectedDecision selected = Assert.IsType<SelectedDecision>(decision);
        Assert.Equal("keep-planning", selected.OptionId);
        Assert.Equal(0, inner.Calls);
    }

    /// <summary>With no one-time refusal on offer, the standing refusal is used.</summary>
    [Fact]
    public async Task Plan_SwitchModeRequest_WithOnlyRejectAlways_FallsBackToIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        PlanModePermissionHandler handler = new(new StubHandler(), "plan", NullLogger<PlanModePermissionHandler>.Instance);
        IReadOnlyList<PermissionOptionInfo> options =
        [
            new PermissionOptionInfo("go", "Go", PermissionOptionKind.AllowOnce),
            new PermissionOptionInfo("never", "Never", PermissionOptionKind.RejectAlways),
        ];

        PermissionDecision decision = await handler.DecideAsync(Request(ToolKind.SwitchMode, options), ct);

        Assert.Equal("never", Assert.IsType<SelectedDecision>(decision).OptionId);
    }

    /// <summary>With no refusal on offer at all, the request is cancelled rather than approved.</summary>
    [Fact]
    public async Task Plan_SwitchModeRequest_WithNoRefusalOffered_IsCancelled()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        PlanModePermissionHandler handler = new(new StubHandler(), "plan", NullLogger<PlanModePermissionHandler>.Instance);
        IReadOnlyList<PermissionOptionInfo> options = [new PermissionOptionInfo("go", "Go", PermissionOptionKind.AllowOnce)];

        PermissionDecision decision = await handler.DecideAsync(Request(ToolKind.SwitchMode, options), ct);

        Assert.IsType<CancelledDecision>(decision);
    }

    /// <summary>Every other kind of request in plan mode goes to the handler beneath, unchanged.</summary>
    [Theory]
    [InlineData(ToolKind.Read)]
    [InlineData(ToolKind.Edit)]
    [InlineData(ToolKind.Execute)]
    [InlineData(ToolKind.Other)]
    public async Task Plan_OtherKinds_AreDelegated(ToolKind kind)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        StubHandler inner = new();
        PlanModePermissionHandler handler = new(inner, "plan", NullLogger<PlanModePermissionHandler>.Instance);

        PermissionDecision decision = await handler.DecideAsync(Request(kind, ExitPlanOptions), ct);

        Assert.Equal(1, inner.Calls);
        Assert.Equal("inner", Assert.IsType<SelectedDecision>(decision).OptionId);
    }

    /// <summary>A switch-mode request is delegated when the Persona is not in plan mode: the guard keeps only a plan Persona in plan mode.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("default")]
    [InlineData("acceptEdits")]
    public async Task NotPlan_SwitchModeRequest_IsDelegated(string? workMode)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        StubHandler inner = new();
        PlanModePermissionHandler handler = new(inner, workMode, NullLogger<PlanModePermissionHandler>.Instance);

        PermissionDecision decision = await handler.DecideAsync(Request(ToolKind.SwitchMode, ExitPlanOptions), ct);

        Assert.Equal(1, inner.Calls);
        Assert.Equal("inner", Assert.IsType<SelectedDecision>(decision).OptionId);
    }

    /// <summary>The id is compared ordinally: a differently cased id is a different mode, so the guard stays inert for it.</summary>
    [Fact]
    public async Task Plan_IdIsOrdinal_DifferentCaseIsNotGuarded()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        StubHandler inner = new();
        PlanModePermissionHandler handler = new(inner, "Plan", NullLogger<PlanModePermissionHandler>.Instance);

        _ = await handler.DecideAsync(Request(ToolKind.SwitchMode, ExitPlanOptions), ct);

        Assert.Equal(1, inner.Calls);
    }

    /// <summary>A refusal is logged at information level, so a plan Persona that never leaves plan mode is explicable from the log.</summary>
    [Fact]
    public async Task Plan_SwitchModeRequest_LogsTheRefusalAtInformation()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingLogger logger = new();
        PlanModePermissionHandler handler = new(new StubHandler(), "plan", logger);

        _ = await handler.DecideAsync(Request(ToolKind.SwitchMode, ExitPlanOptions), ct);

        Assert.Equal([LogLevel.Information], logger.Levels);
    }

    private static PermissionRequestContext Request(ToolKind kind, IReadOnlyList<PermissionOptionInfo> options)
    {
        return new PermissionRequestContext(
            "session-1",
            new ToolCallInfo("call-1", "Approve Plan", kind, ToolCallStatus.Pending, "{\"plan\":\"1. do it\"}"),
            options);
    }

    /// <summary>A handler beneath the guard that counts its calls and approves with a recognisable option id.</summary>
    private sealed class StubHandler : IPermissionHandler
    {
        public int Calls { get; private set; }

        public Task<PermissionDecision> DecideAsync(PermissionRequestContext context, CancellationToken cancellationToken)
        {
            this.Calls++;
            return Task.FromResult<PermissionDecision>(new SelectedDecision("inner"));
        }
    }

    /// <summary>Records the level of every entry it is given.</summary>
    private sealed class RecordingLogger : ILogger<PlanModePermissionHandler>
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
