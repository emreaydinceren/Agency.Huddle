using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Waiting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>
/// <see cref="HelperDispatcher"/> waits per key, runs one judgement per key at a time under a concurrency limit, gates on
/// enabled and Adapters-on, reuses Verdicts by content, and never lets a Helper take it down
/// (Helpers Spec §6.3, plan HL-8).
/// </summary>
public sealed class HelperDispatcherTests : IDisposable
{
    private readonly TempDataDir dir = new();
    private readonly FakeHelperProcessRunner process = new();
    private readonly ScriptedHelper helper = new();
    private readonly FakePromptSource prompts = new();
    private readonly FiringTimeProvider clock = new();

    /// <summary>Sets the two prompts the scripted Helper renders.</summary>
    public HelperDispatcherTests()
    {
        this.prompts.SetOverride("test.system", "You judge.");
        this.prompts.SetOverride("test.task", "Judge: {{input}}");
    }

    /// <summary>Builds the dispatcher over real stores on the temp directory and the firing clock.</summary>
    /// <param name="configure">Changes the options, or <see langword="null"/>.</param>
    private (HelperDispatcher Dispatcher, HelperStatusStore Status, HelperSettingsStore Settings, HelperUsageStore Usage) Build(Action<TeamOptions>? configure = null)
    {
        IOptions<TeamOptions> options = this.dir.Options(team =>
        {
            team.Acp.Enabled = true;
            team.Acp.Adapters = [new AdapterProfileOptions { Id = "claude", Command = "node", Args = ["adapter.js"], UsesToolNamePrefix = false }];
            configure?.Invoke(team);
        });
        HelperUsageStore usage = new(options);
        HelperRunner runner = new(this.process, new AdapterCatalog(options), this.prompts, new HelperBudget(usage, this.clock), options, NullLogger<HelperRunner>.Instance, this.clock);
        HelperSettingsStore settings = new(options);
        HelperStatusStore status = new(options);
        HelperDispatcher dispatcher = new(
            [new HelperInvoker<string, ScriptedVerdict>(this.helper, runner)],
            new HelperSettingsResolver(settings, options, NullLogger<HelperSettingsResolver>.Instance),
            status,
            options,
            NullLogger<HelperDispatcher>.Instance,
            this.clock);
        return (dispatcher, status, settings, usage);
    }

    /// <summary>Removes the temp data directory.</summary>
    public void Dispose() => this.dir.Dispose();

    /// <summary>Waits until the Helper has applied <paramref name="count"/> Verdicts.</summary>
    /// <param name="count">How many applies.</param>
    private Task AppliedAsync(int count) =>
        Eventually.UntilAsync(() => this.helper.Applied.Count >= count, TestContext.Current.CancellationToken, TestBudgets.Standard);

    /// <summary>Nothing runs until the wait has passed; then the input is judged and applied once.</summary>
    [Fact]
    public async Task Enqueue_RunsAfterTheWait()
    {
        HelperDispatcher dispatcher = this.Build().Dispatcher;

        dispatcher.Enqueue(this.helper.Id, "RSH-1", "hello");
        this.clock.Advance(TimeSpan.FromSeconds(9));
        Assert.Equal(0, this.process.Calls);
        this.clock.Advance(TimeSpan.FromSeconds(2));
        await this.AppliedAsync(1);

        Assert.Equal(1, this.process.Calls);
        Assert.Equal(("RSH-1", "hello"), (this.helper.Applied[0].Key, this.helper.Applied[0].Input));
    }

    /// <summary>Each save restarts the wait and the last input wins, so three saves are one judgement.</summary>
    [Fact]
    public async Task Enqueue_RepeatedSaves_AreOneJudgementOfTheLastInput()
    {
        HelperDispatcher dispatcher = this.Build().Dispatcher;

        dispatcher.Enqueue(this.helper.Id, "RSH-1", "one");
        this.clock.Advance(TimeSpan.FromSeconds(6));
        dispatcher.Enqueue(this.helper.Id, "RSH-1", "two");
        this.clock.Advance(TimeSpan.FromSeconds(6));
        dispatcher.Enqueue(this.helper.Id, "RSH-1", "three");
        this.clock.Advance(TimeSpan.FromSeconds(6));
        Assert.Equal(0, this.process.Calls);
        this.clock.Advance(TimeSpan.FromSeconds(5));
        await this.AppliedAsync(1);

        Assert.Equal(1, this.process.Calls);
        Assert.Equal("three", this.helper.Applied[0].Input);
    }

    /// <summary>A key saved without a pause is still judged at six times the wait from the first save.</summary>
    [Fact]
    public async Task Enqueue_ConstantSaves_AreJudgedAtSixTimesTheWait()
    {
        HelperDispatcher dispatcher = this.Build().Dispatcher;
        dispatcher.Enqueue(this.helper.Id, "RSH-1", "v0");
        for (int i = 1; i <= 6; i++)
        {
            this.clock.Advance(TimeSpan.FromSeconds(9));
            dispatcher.Enqueue(this.helper.Id, "RSH-1", $"v{i}");
        }

        Assert.Equal(0, this.process.Calls);
        this.clock.Advance(TimeSpan.FromSeconds(6));
        await this.AppliedAsync(1);

        Assert.Equal("v6", this.helper.Applied[0].Input);
    }

    /// <summary>A save made while its key is being judged waits, then runs after the first judgement ends.</summary>
    [Fact]
    public async Task Enqueue_WhileRunning_RunsAfterTheFirstEnds()
    {
        TaskCompletionSource release = new();
        this.process.Hold = release.Task;
        HelperDispatcher dispatcher = this.Build().Dispatcher;
        dispatcher.Enqueue(this.helper.Id, "RSH-1", "first");
        this.clock.Advance(TimeSpan.FromSeconds(11));
        await Eventually.UntilAsync(() => this.process.Calls == 1, TestContext.Current.CancellationToken, TestBudgets.Standard);

        dispatcher.Enqueue(this.helper.Id, "RSH-1", "second");
        this.clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(1, this.process.Calls);
        release.SetResult();
        await this.AppliedAsync(1);
        await Eventually.UntilAsync(() => this.process.Calls == 1 && this.helper.Applied.Count == 1, TestContext.Current.CancellationToken, TestBudgets.Standard);
        this.clock.Advance(TimeSpan.FromSeconds(11));
        await this.AppliedAsync(2);

        Assert.Equal(["first", "second"], this.helper.Applied.Select(static applied => applied.Input));
    }

    /// <summary>No more than the configured number of runs happen at once, across keys.</summary>
    [Fact]
    public async Task Enqueue_ManyKeys_RespectsTheConcurrencyLimit()
    {
        TaskCompletionSource release = new();
        this.process.Hold = release.Task;
        HelperDispatcher dispatcher = this.Build(static team => team.HelperConcurrency = 2).Dispatcher;
        foreach (string key in new[] { "k1", "k2", "k3" })
        {
            dispatcher.Enqueue(this.helper.Id, key, $"input {key}");
        }

        this.clock.Advance(TimeSpan.FromSeconds(11));
        await Eventually.UntilAsync(() => this.process.Calls == 2, TestContext.Current.CancellationToken, TestBudgets.Standard);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal(2, this.process.Calls);
        release.SetResult();
        await this.AppliedAsync(3);
        Assert.Equal(2, this.process.PeakInFlight);
    }

    /// <summary>A Task with identical content reuses the first Verdict: no second call, and the Helper still applies it.</summary>
    [Fact]
    public async Task Enqueue_SameContentOnAnotherKey_AppliesFromTheCache()
    {
        HelperDispatcher dispatcher = this.Build().Dispatcher;
        dispatcher.Enqueue(this.helper.Id, "k1", "same text");
        this.clock.Advance(TimeSpan.FromSeconds(11));
        await this.AppliedAsync(1);

        dispatcher.Enqueue(this.helper.Id, "k2", "same text");
        this.clock.Advance(TimeSpan.FromSeconds(11));
        await this.AppliedAsync(2);

        Assert.Equal(1, this.process.Calls);
        Assert.Equal(["k1", "k2"], this.helper.Applied.Select(static applied => applied.Key));
    }

    /// <summary>A Helper the Human turned off runs nothing.</summary>
    [Fact]
    public async Task Enqueue_HelperDisabled_RunsNothing()
    {
        (HelperDispatcher dispatcher, _, HelperSettingsStore settings, _) = this.Build();
        settings.Save(this.helper.Id, new HelperSettingsRow(false, null, null, null, 50));

        dispatcher.Enqueue(this.helper.Id, "k1", "text");
        this.clock.Advance(TimeSpan.FromSeconds(11));
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal(0, this.process.Calls);
        Assert.Empty(this.helper.Applied);
    }

    /// <summary>With Adapters off in this installation no Helper runs.</summary>
    [Fact]
    public async Task Enqueue_AdaptersOff_RunsNothing()
    {
        HelperDispatcher dispatcher = this.Build(static team => team.Acp.Enabled = false).Dispatcher;

        dispatcher.Enqueue(this.helper.Id, "k1", "text");
        this.clock.Advance(TimeSpan.FromSeconds(11));
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal(0, this.process.Calls);
    }

    /// <summary>An ApplyAsync that throws is recorded on the card and not raised; the next judgement still runs.</summary>
    [Fact]
    public async Task Enqueue_ApplyThrows_IsRecordedAndDispatcherSurvives()
    {
        (HelperDispatcher dispatcher, HelperStatusStore status, _, _) = this.Build();
        this.helper.ApplyHandler = static (_, input, _) => string.Equals(input, "explode", StringComparison.Ordinal)
            ? throw new InvalidOperationException("the store is locked")
            : new HelperApplied.Changed("Turned it on");
        dispatcher.Enqueue(this.helper.Id, "k1", "explode");
        this.clock.Advance(TimeSpan.FromSeconds(11));
        await Eventually.UntilAsync(() => status.Get(this.helper.Id).LastError is not null, TestContext.Current.CancellationToken, TestBudgets.Standard);

        dispatcher.Enqueue(this.helper.Id, "k2", "fine");
        this.clock.Advance(TimeSpan.FromSeconds(11));
        await Eventually.UntilAsync(() => status.Get(this.helper.Id).LastResult is not null, TestContext.Current.CancellationToken, TestBudgets.Standard);

        HelperStatus after = status.Get(this.helper.Id);
        Assert.Equal("the store is locked", after.LastError);
        Assert.Equal("Turned it on", after.LastResult);
        Assert.Equal(0, after.ConsecutiveFailures);
    }

    /// <summary>A refused change is stored as a failure with the refusal as the error.</summary>
    [Fact]
    public async Task Enqueue_ApplyRefused_IsStoredAsAFailure()
    {
        (HelperDispatcher dispatcher, HelperStatusStore status, _, _) = this.Build();
        this.helper.ApplyHandler = static (_, _, _) => new HelperApplied.Refused("Only You can turn off the gate.");

        dispatcher.Enqueue(this.helper.Id, "k1", "text");
        this.clock.Advance(TimeSpan.FromSeconds(11));
        await Eventually.UntilAsync(() => status.Get(this.helper.Id).LastError is not null, TestContext.Current.CancellationToken, TestBudgets.Standard);

        HelperStatus stored = status.Get(this.helper.Id);
        Assert.Equal("Only You can turn off the gate.", stored.LastError);
        Assert.Equal(1, stored.ConsecutiveFailures);
    }

    /// <summary>An unparseable answer is stored with the card's wording.</summary>
    [Fact]
    public async Task Enqueue_ProseAnswer_IsStoredWithTheCardWording()
    {
        (HelperDispatcher dispatcher, HelperStatusStore status, _, _) = this.Build();
        this.process.Text = "I think so.";

        dispatcher.Enqueue(this.helper.Id, "k1", "text");
        this.clock.Advance(TimeSpan.FromSeconds(11));
        await Eventually.UntilAsync(() => status.Get(this.helper.Id).LastError is not null, TestContext.Current.CancellationToken, TestBudgets.Standard);

        Assert.Equal("The answer wasn't the JSON the prompt asks for (no JSON object)", status.Get(this.helper.Id).LastError);
    }

    /// <summary>When the cap is reached the input is skipped and counted, and the card's status is untouched.</summary>
    [Fact]
    public async Task Enqueue_OverBudget_IsSkippedAndCounted()
    {
        (HelperDispatcher dispatcher, HelperStatusStore status, HelperSettingsStore settings, HelperUsageStore usage) = this.Build();
        settings.Save(this.helper.Id, new HelperSettingsRow(true, null, null, null, 1));
        dispatcher.Enqueue(this.helper.Id, "k1", "first");
        this.clock.Advance(TimeSpan.FromSeconds(11));
        await this.AppliedAsync(1);
        await Eventually.UntilAsync(() => status.Get(this.helper.Id).LastResult is not null, TestContext.Current.CancellationToken, TestBudgets.Standard);
        HelperStatus before = status.Get(this.helper.Id);

        dispatcher.Enqueue(this.helper.Id, "k2", "second");
        this.clock.Advance(TimeSpan.FromSeconds(11));
        string day = this.clock.GetLocalNow().Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        await Eventually.UntilAsync(() => usage.Get(this.helper.Id, day).Skipped == 1, TestContext.Current.CancellationToken, TestBudgets.Standard);

        Assert.Equal(1, this.process.Calls);
        Assert.Equal(before, status.Get(this.helper.Id));
    }

    /// <summary>Stopping drops what is still waiting.</summary>
    [Fact]
    public async Task StopAsync_DropsPendingInputs()
    {
        HelperDispatcher dispatcher = this.Build().Dispatcher;
        dispatcher.Enqueue(this.helper.Id, "k1", "text");

        await dispatcher.StopAsync(TestContext.Current.CancellationToken);
        this.clock.Advance(TimeSpan.FromSeconds(11));
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal(0, this.process.Calls);
    }

    /// <summary>An unregistered Helper id is refused at once.</summary>
    [Fact]
    public void Enqueue_UnknownHelper_Throws()
    {
        HelperDispatcher dispatcher = this.Build().Dispatcher;

        Assert.Throws<InvalidOperationException>(() => dispatcher.Enqueue(new HelperId("Nope"), "k1", "text"));
    }
}
