using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MudBlazor;
using Agency.Huddle.App;
using Agency.Huddle.App.Components.Shared.AgentCards;
using Agency.Huddle.App.Data;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Tests for <see cref="AgentCardBase.RunBusyAsync"/> (UX-R01), driven through a probe card so the
/// base's own rules are pinned apart from any one card's markup.
/// </summary>
public sealed class AgentCardBaseTests
{
    /// <summary>A card with no markup that exposes the base's protected members.</summary>
    public sealed class ProbeCard : AgentCardBase
    {
        /// <summary>When set, <see cref="LoadAsync"/> throws, standing in for a reload that fails.</summary>
        public bool FailLoad { get; set; }

        /// <summary>Whether any call of the card is running.</summary>
        public bool IsRunning => this.Busy;

        /// <summary>Runs <paramref name="work"/> through <see cref="AgentCardBase.RunBusyAsync"/>.</summary>
        /// <param name="work">The call.</param>
        /// <param name="key">The busy key.</param>
        /// <param name="allowParallel">Whether the call may overlap another key.</param>
        public Task RunAsync(Func<Task> work, string key = "", bool allowParallel = false)
        {
            return this.RunBusyAsync(work, key, allowParallel);
        }

        /// <inheritdoc />
        protected override void Subscribe()
        {
        }

        /// <inheritdoc />
        protected override void Unsubscribe()
        {
        }

        /// <inheritdoc />
        protected override Task LoadAsync()
        {
            return this.FailLoad ? throw new InvalidOperationException("The reload failed.") : Task.CompletedTask;
        }
    }

    /// <summary>Renders a probe card over a real directory.</summary>
    /// <param name="ctx">The bUnit context.</param>
    /// <param name="dataDir">The data folder the directory uses.</param>
    /// <param name="ct">Cancels the directory set-up.</param>
    private static async Task<IRenderedComponent<ProbeCard>> RenderProbeAsync(MudBunitContext ctx, TempDataDir dataDir, CancellationToken ct)
    {
        IOptions<TeamOptions> options = dataDir.Options();
        SqliteTeamDirectory directory = new(options);
        await directory.InitializeAsync("You", ct);
        ctx.Services.AddSingleton<ITeamDirectory>(directory);
        return ctx.Render<ProbeCard>(parameters => parameters.Add(card => card.RoomId, "room"));
    }

    /// <summary>A reload that throws after the call is reported by the keyed snackbar, and the card is unlocked rather than stuck busy.</summary>
    [Fact]
    public async Task ReloadThrows_ReportsKeyedSnackbarAndReleasesBusy()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        await using MudBunitContext ctx = new();
        IRenderedComponent<ProbeCard> cut = await RenderProbeAsync(ctx, dataDir, ct);
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();
        cut.Instance.FailLoad = true;

        await cut.InvokeAsync(() => cut.Instance.RunAsync(() => Task.CompletedTask));

        Assert.Equal("The reload failed.", Assert.Single(snackbar.ShownSnackbars).Message);
        Assert.False(cut.Instance.IsRunning);
        Assert.Null(snackbar.Add("A repeat", Severity.Error, configure: null, key: "agent-card-error"));
    }

    /// <summary>A second action clicked while the first runs does nothing, so Approve and Decline in one render never both run.</summary>
    [Fact]
    public async Task SecondActionWhileBusy_IsIgnored()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        await using MudBunitContext ctx = new();
        IRenderedComponent<ProbeCard> cut = await RenderProbeAsync(ctx, dataDir, ct);
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int secondRuns = 0;
        Task first = cut.InvokeAsync(() => cut.Instance.RunAsync(() => gate.Task, "approve"));

        await cut.InvokeAsync(() => cut.Instance.RunAsync(
            () =>
            {
                secondRuns++;
                return Task.CompletedTask;
            },
            "decline"));
        gate.SetResult();
        await first.WaitAsync(ct);

        Assert.Equal(0, secondRuns);
    }

    /// <summary>A card whose actions are independent (Elicitation's forms) opts out, so a second key runs beside the first.</summary>
    [Fact]
    public async Task AllowParallel_RunsASecondKeyBesideTheFirst()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        await using MudBunitContext ctx = new();
        IRenderedComponent<ProbeCard> cut = await RenderProbeAsync(ctx, dataDir, ct);
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int secondRuns = 0;
        Task first = cut.InvokeAsync(() => cut.Instance.RunAsync(() => gate.Task, "one", allowParallel: true));

        await cut.InvokeAsync(() => cut.Instance.RunAsync(
            () =>
            {
                secondRuns++;
                return Task.CompletedTask;
            },
            "two",
            allowParallel: true));
        gate.SetResult();
        await first.WaitAsync(ct);

        Assert.Equal(1, secondRuns);
    }
}
