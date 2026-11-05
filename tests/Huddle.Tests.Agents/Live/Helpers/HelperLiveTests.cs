using System.Diagnostics;
using System.Globalization;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.AgentsMdChanges;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Helpers.AgentsMdChange;
using Agency.Huddle.App.Helpers.NeedsHuman;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.AgentsMdChanges;
using Agency.Huddle.Tests.Feed;
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
    private readonly HelperSettingsResolver resolver;
    private readonly AgentsMdLedgerFixture ledgerFixture = new();

    /// <summary>Builds the real runner over the real Claude profile, recording every Turn it produces.</summary>
    public HelperLiveTests()
    {
        TeamOptions team = this.harness.Options.Value;
        team.Acp.Enabled = true;
        HelperSettingsStore settingsStore = new(this.harness.Options);
        this.resolver = new(settingsStore, this.harness.Options, NullLogger<HelperSettingsResolver>.Instance);
        this.helper = new NeedsHumanHelper(
            this.harness.Store,
            this.harness.Service,
            new NeedsHumanOverrideStore(this.harness.Options, TimeProvider.System),
            this.resolver);
        this.settings = this.resolver.For(this.helper) with { DailyCap = 100, TimeoutSeconds = 120 };
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
        this.ledgerFixture.Dispose();
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

    /// <summary>LIVE-AMD-1 (Spec C-1): a one-line edit on a 1 KB AGENTS.md, judged three times; each run's time and outcome are logged.</summary>
    [Fact(Timeout = 600_000, SkipUnless = nameof(LiveHelpers.Enabled), SkipType = typeof(LiveHelpers), Skip = Skip)]
    public async Task LiveAmd1_OneLineEdit_On1KbFile() =>
        await this.MeasureAsync("one-line-1kb", 1000, 1, 0, 0, TestContext.Current.CancellationToken);

    /// <summary>LIVE-AMD-2 (Spec C-1): a section rewrite on an 8 KB AGENTS.md, judged three times; each run's time and outcome are logged.</summary>
    [Fact(Timeout = 600_000, SkipUnless = nameof(LiveHelpers.Enabled), SkipType = typeof(LiveHelpers), Skip = Skip)]
    public async Task LiveAmd2_SectionRewrite_On8KbFile() =>
        await this.MeasureAsync("section-8kb", 8000, 2, 10, 1, TestContext.Current.CancellationToken);

    /// <summary>LIVE-AMD-3 (Spec C-1): a rewrite of many sections at the 16,000-character cap, judged three times; each run's time and outcome are logged.</summary>
    [Fact(Timeout = 600_000, SkipUnless = nameof(LiveHelpers.Enabled), SkipType = typeof(LiveHelpers), Skip = Skip)]
    public async Task LiveAmd3_Rewrite_AtTheCap() =>
        await this.MeasureAsync("cap-16000", 16000, 2, 10, 20, TestContext.Current.CancellationToken);

    /// <summary>One AGENTS.md section of about 230 characters; <paramref name="mode"/> 0 is the base text, 1 changes one line, 2 rewrites every line.</summary>
    /// <param name="index">The section number.</param>
    /// <param name="mode">How much of the section differs.</param>
    private static string Section(int index, int mode) => mode switch
    {
        0 => $"## Area {index}\n- Merge after one approval in area {index}.\n- Name every branch for area {index} after its ticket.\n- Keep functions in area {index} under forty lines.\n- Write a test with every change to area {index}.\n\n",
        1 => $"## Area {index}\n- Merge only after two approvals in area {index}.\n- Name every branch for area {index} after its ticket.\n- Keep functions in area {index} under forty lines.\n- Write a test with every change to area {index}.\n\n",
        _ => $"## Area {index}\n- Never merge area {index} on a Friday, and tag the release owner.\n- Branches for area {index} are named feature/ticket-number.\n- Functions in area {index} stay under thirty lines.\n- Every change to area {index} needs a test and a changelog note.\n\n",
    };

    /// <summary>Builds a document of <paramref name="chars"/> characters whose sections from <paramref name="from"/> for <paramref name="count"/> use <paramref name="mode"/>.</summary>
    /// <param name="chars">The length.</param>
    /// <param name="mode">The section mode used in the edited range.</param>
    /// <param name="from">The first edited section.</param>
    /// <param name="count">How many sections are edited.</param>
    private static string Document(int chars, int mode, int from, int count)
    {
        System.Text.StringBuilder text = new();
        for (int i = 0; text.Length < chars; i++)
        {
            text.Append(Section(i, i >= from && i < from + count ? mode : 0));
        }

        return text.ToString(0, chars).TrimEnd() + "\n";
    }

    /// <summary>Runs the real AGENTS.md Changes Helper <see cref="Runs"/> times on one edit, applies each answer and appends a line per run to the log.</summary>
    /// <param name="name">The case.</param>
    /// <param name="chars">The size of the file.</param>
    /// <param name="mode">The section mode of the edit (1 changes one line, 2 rewrites).</param>
    /// <param name="from">The first edited section.</param>
    /// <param name="count">How many sections are edited; 0 with mode 1 edits section 0 by one line.</param>
    /// <param name="ct">Cancels the runs.</param>
    private async Task MeasureAsync(string name, int chars, int mode, int from, int count, CancellationToken ct)
    {
        AgentsMdChangeHelper changer = new(this.ledgerFixture.Ledger, new RecordingFeedPublisher(), this.ledgerFixture.Options, NullLogger<AgentsMdChangeHelper>.Instance);
        HelperSettings changeSettings = this.resolver.For(changer) with { DailyCap = 100, TimeoutSeconds = 120 };
        string log = @"E:\Repos\Huddle2\Conversation\agentsmd\reports\5.6-runs.tsv";
        string oldText = Document(chars, 0, 0, 0);
        string newText = count == 0 ? Document(chars, mode, 0, 1) : Document(chars, mode, from, count);
        int ok = 0;
        List<string> lines = [];
        for (int run = 1; run <= Runs; run++)
        {
            string relative = $"{name}-{run}/AGENTS.md";
            string path = Path.Combine(this.ledgerFixture.Options.Value.DataDir, this.ledgerFixture.Options.Value.Teams.Dir, relative);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
            await File.WriteAllTextAsync(path, oldText, ct);
            _ = this.ledgerFixture.Ledger.Observe(new AgentsMdFile(AgentsMdLevel.Team, $"{name}-{run}", null, path, relative));
            await File.WriteAllTextAsync(path, newText, ct);
            string oldBody = AgentsMdBody.Parse(oldText).Text;
            string newBody = AgentsMdBody.Parse(newText).Text;
            AgentsMdChangeInput input = new(relative, "Team", AgentsMdBody.Parse(oldText).Hash, AgentsMdBody.Parse(newText).Hash, oldBody, newBody, AgentsMdComparison.Between(oldBody, newBody));

            int turns = this.process.Turns.Count;
            long started = Stopwatch.GetTimestamp();
            HelperOutcome<AgentsMdChangeVerdict> outcome = await this.runner.RunAsync(changer, changeSettings, input, ct);
            double seconds = Stopwatch.GetElapsedTime(started).TotalSeconds;

            string validated = "n/a";
            string applied = "n/a";
            if (outcome is HelperOutcome<AgentsMdChangeVerdict>.Ok verdictOk)
            {
                validated = changer.Validate(verdictOk.Verdict) ?? "ok";
                applied = (await changer.ApplyAsync("live", input, verdictOk.Verdict, ct)).GetType().Name;
                ok += validated == "ok" && applied != "Refused" ? 1 : 0;
            }

            HelperTurn? turn = this.process.Turns.Count > turns ? this.process.Turns[^1] : null;
            string line = Describe(name, run, seconds, outcome, validated, applied, turn);
            await File.AppendAllTextAsync(log, line, ct);
            lines.Add(line);
        }

        Assert.True(ok == Runs, string.Join('\n', lines));
    }

    /// <summary>One run's record: a tab-separated line, and for an unparseable answer its problem, the head and tail of the full text.</summary>
    /// <param name="name">The case.</param>
    /// <param name="run">The run number.</param>
    /// <param name="seconds">How long the call took.</param>
    /// <param name="outcome">What the runner returned.</param>
    /// <param name="validated">The result of Validate, or n/a.</param>
    /// <param name="applied">The type of the applied result, or n/a.</param>
    /// <param name="turn">The adapter's Turn, which holds the whole answer and the cost.</param>
    private static string Describe(string name, int run, double seconds, HelperOutcome<AgentsMdChangeVerdict> outcome, string validated, string applied, HelperTurn? turn)
    {
        string cost = turn?.Cost is { } c ? $"{c.Amount} {c.Currency}" : "none";
        string parsed = outcome is HelperOutcome<AgentsMdChangeVerdict>.Ok ? "yes" : "no";
        string quotes = applied == "Changed" ? "applied" : applied;
        System.Text.StringBuilder record = new(string.Create(CultureInfo.InvariantCulture, $"{name}\trun{run}\t{seconds:F1}s\t{outcome.GetType().Name}\tparsed={parsed}\tvalidated={validated}\tquotesApplied={quotes}\tcost={cost}\n"));
        if (outcome is HelperOutcome<AgentsMdChangeVerdict>.Failed failed)
        {
            record.Append("  reason: ").Append(failed.Reason).Append('\n');
        }
        else if (outcome is HelperOutcome<AgentsMdChangeVerdict>.Unparseable unparseable)
        {
            string full = turn?.Text ?? unparseable.Text;
            record.Append("  problem: ").Append(unparseable.Problem).Append('\n');
            record.Append("  fullLength: ").Append(full.Length.ToString(CultureInfo.InvariantCulture)).Append('\n');
            record.Append("  textHead2000: ").Append(full.Length > 2000 ? full[..2000] : full).Append('\n');
            record.Append("  textTail300: ").Append(full.Length > 300 ? full[^300..] : full).Append('\n');
        }

        return record.ToString();
    }

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
