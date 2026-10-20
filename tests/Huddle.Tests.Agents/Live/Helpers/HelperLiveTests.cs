using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Helpers.NeedsHuman;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Tools;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Live.Helpers;

/// <summary>
/// The real <c>AdapterProcessHelperRunner</c> against the real Claude adapter on Haiku at low Effort, driven through the
/// Needs Human Helper's shipped prompts. Paid: it never runs unless <c>TEAM_E2E_HELPERS=1</c>, and it makes about seven calls
/// at a fraction of a cent each. It proves what the fake runner cannot: that the toolless session settings keep tools and
/// connectors out, that a real answer parses, and that the Verdict is the right way round for a clear case each way.
/// </summary>
public sealed class HelperLiveTests : IDisposable
{
    private const string Skip = "Set TEAM_E2E_HELPERS=1 to run (paid)";
    private const int Runs = 3;
    private const int MinPasses = 2;

    private readonly TempDataDir dir = new();
    private readonly TaskToolHarness harness = new();
    private readonly RecordingProcessRunner process;
    private readonly HelperRunner runner;
    private readonly NeedsHumanHelper helper;
    private readonly HelperSettings settings;

    /// <summary>Builds the real runner over the real Claude profile, recording every Turn it produces.</summary>
    public HelperLiveTests()
    {
        TeamOptions team = this.harness.Options.Value;
        team.Acp.Enabled = true;
        HelperSettingsStore settingsStore = new(this.harness.Options);
        HelperSettingsResolver resolver = new(settingsStore, this.harness.Options, NullLogger<HelperSettingsResolver>.Instance);
        this.helper = new NeedsHumanHelper(
            this.harness.Store,
            this.harness.Service,
            new NeedsHumanOverrideStore(this.harness.Options, TimeProvider.System),
            resolver);
        this.settings = resolver.For(this.helper) with { DailyCap = 100, TimeoutSeconds = 120 };
        this.process = new RecordingProcessRunner(new AdapterProcessHelperRunner(this.harness.Options, NullLoggerFactory.Instance));
        this.runner = new HelperRunner(
            this.process,
            new AdapterCatalog(this.harness.Options),
            new FakePromptSource(),
            new HelperBudget(new HelperUsageStore(this.harness.Options)),
            this.harness.Options,
            NullLogger<HelperRunner>.Instance);
    }

    /// <summary>Removes the temp directories.</summary>
    public void Dispose()
    {
        this.harness.Dispose();
        this.dir.Dispose();
    }

    /// <summary>LIVE-HELPERS-1: one real call returns a parseable Verdict, uses no tool, offers the configured Model and costs well under two cents.</summary>
    [Fact(Timeout = 300_000, SkipUnless = nameof(LiveHelpers.Enabled), SkipType = typeof(LiveHelpers), Skip = Skip)]
    public async Task Live1_OneCall_IsParseable_ToolFree_AndCheap()
    {
        HelperOutcome<NeedsHumanVerdict> outcome = await this.runner.RunAsync(
            this.helper, this.settings, Input("Reply to Acme about the renewal price"), TestContext.Current.CancellationToken);

        Assert.IsType<HelperOutcome<NeedsHumanVerdict>.Ok>(outcome);
        HelperTurn turn = Assert.Single(this.process.Turns);
        Assert.True(turn.ModelOffered);
        Assert.Equal(0, turn.ToolCallsRefused);
        Assert.NotNull(turn.Cost);
        Assert.True(turn.Cost.Amount < 0.02m, $"One call cost {turn.Cost.Amount} {turn.Cost.Currency}; the toolless session should cost a fraction of a cent.");
    }

    /// <summary>LIVE-HELPERS-2: a Task that sends a message to a customer is judged to need the Human, in most of three runs.</summary>
    [Fact(Timeout = 600_000, SkipUnless = nameof(LiveHelpers.Enabled), SkipType = typeof(LiveHelpers), Skip = Skip)]
    public async Task Live2_OutwardTask_NeedsHuman() =>
        Assert.True(await this.PassesAsync("Reply to Acme about the renewal price", expectNeedsHuman: true, TestContext.Current.CancellationToken) >= MinPasses);

    /// <summary>LIVE-HELPERS-3: a Task that stays inside the team is judged not to need the Human, in most of three runs.</summary>
    [Fact(Timeout = 600_000, SkipUnless = nameof(LiveHelpers.Enabled), SkipType = typeof(LiveHelpers), Skip = Skip)]
    public async Task Live3_InternalTask_DoesNotNeedHuman() =>
        Assert.True(await this.PassesAsync("Summarise the interview notes for the team wiki", expectNeedsHuman: false, TestContext.Current.CancellationToken) >= MinPasses);

    /// <summary>The input a trigger would capture for a Task of this title.</summary>
    /// <param name="title">The Task's title.</param>
    private static NeedsHumanInput Input(string title) => new(new TaskId("RSH", 1), "fingerprint", title, string.Empty, "Research", []);

    /// <summary>Runs the Helper <see cref="Runs"/> times on one title and counts the runs that gave the expected answer.</summary>
    /// <param name="title">The Task's title.</param>
    /// <param name="expectNeedsHuman">The answer a person would give.</param>
    /// <param name="ct">Cancels the runs.</param>
    private async Task<int> PassesAsync(string title, bool expectNeedsHuman, CancellationToken ct)
    {
        int passes = 0;
        for (int i = 0; i < Runs; i++)
        {
            HelperOutcome<NeedsHumanVerdict> outcome = await this.runner.RunAsync(this.helper, this.settings, Input(title), ct);
            if (outcome is HelperOutcome<NeedsHumanVerdict>.Ok ok && ok.Verdict.NeedsHuman == expectNeedsHuman)
            {
                passes++;
            }
        }

        return passes;
    }

    /// <summary>Wraps the real process runner and keeps each Turn, so a test can read what the session did.</summary>
    /// <param name="inner">The real runner.</param>
    private sealed class RecordingProcessRunner(IHelperProcessRunner inner) : IHelperProcessRunner
    {
        private readonly List<HelperTurn> turns = [];

        /// <summary>Every Turn the real runner returned.</summary>
        public IReadOnlyList<HelperTurn> Turns
        {
            get
            {
                lock (this.turns)
                {
                    return [.. this.turns];
                }
            }
        }

        /// <inheritdoc/>
        public async Task<HelperTurn> RunAsync(AgentProcessOptions processOptions, HelperSessionRequest request, CancellationToken cancellationToken)
        {
            HelperTurn turn = await inner.RunAsync(processOptions, request, cancellationToken);
            lock (this.turns)
            {
                this.turns.Add(turn);
            }

            return turn;
        }
    }
}
