using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.Tests.Acp.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>
/// <see cref="HelperRunner"/> turns a prompt into a parsed Verdict and reports every failure as an outcome
/// (Helpers Spec §6.2, plan HL-3).
/// </summary>
public sealed class HelperRunnerTests : IDisposable
{
    private static readonly HelperSettings Settings = new(true, null, null, null, 50, 10, 10, 60);

    private readonly TempDataDir dir = new();
    private readonly FakeHelperProcessRunner process = new();
    private readonly ScriptedHelper helper = new();
    private readonly FakePromptSource prompts = new();
    private readonly FiringTimeProvider clock = new();

    /// <summary>Sets the two prompts the scripted Helper renders.</summary>
    public HelperRunnerTests()
    {
        this.prompts.SetOverride("test.system", "You judge.");
        this.prompts.SetOverride("test.task", "Judge: {{input}}");
    }

    /// <summary>Builds the runner over a configured Adapter that resolves to a launch command.</summary>
    /// <param name="resolvable">False builds an Adapter with no launch command, as when it is not installed.</param>
    private HelperRunner Build(bool resolvable = true)
    {
        IOptions<TeamOptions> options =this.dir.Options(team => team.Acp.Adapters =
        [
            new AdapterProfileOptions
            {
                Id = "claude",
                DisplayName = "Claude",
                Command = "node",
                Args = resolvable ? ["adapter.js"] : null,
                UsesToolNamePrefix = false,
            },
        ]);
        HelperBudget budget = new(new HelperUsageStore(options), this.clock);
        return new HelperRunner(this.process, new AdapterCatalog(options), this.prompts, budget, options, NullLogger<HelperRunner>.Instance, this.clock);
    }

    /// <summary>Removes the temp data directory.</summary>
    public void Dispose() => this.dir.Dispose();

    /// <summary>A JSON object that parses and validates is an Ok Verdict, and the prompts were rendered into the request.</summary>
    [Fact]
    public async Task RunAsync_ValidJson_IsOkAndRendersPrompts()
    {
        HelperOutcome<ScriptedVerdict> outcome = await this.Build().RunAsync(this.helper, Settings, "hello", TestContext.Current.CancellationToken);

        ScriptedVerdict verdict = Assert.IsType<HelperOutcome<ScriptedVerdict>.Ok>(outcome).Verdict;
        Assert.Equal(new ScriptedVerdict(true, "ok"), verdict);
        HelperSessionRequest request = Assert.Single(this.process.Requests);
        Assert.Equal("You judge.", request.SystemPrompt);
        Assert.Equal("Judge: hello", request.Prompt);
        Assert.EndsWith(Path.Combine("helpers", "work"), request.Cwd, StringComparison.Ordinal);
    }

    /// <summary>A Markdown code fence around the JSON is stripped.</summary>
    [Fact]
    public async Task RunAsync_FencedJson_IsOk()
    {
        this.process.Text = "Sure!\n```json\n{\"yes\": false, \"why\": \"internal\"}\n```\n";

        HelperOutcome<ScriptedVerdict> outcome = await this.Build().RunAsync(this.helper, Settings, "x", TestContext.Current.CancellationToken);

        Assert.Equal(new ScriptedVerdict(false, "internal"), Assert.IsType<HelperOutcome<ScriptedVerdict>.Ok>(outcome).Verdict);
    }

    /// <summary>Prose with no JSON object is Unparseable.</summary>
    [Fact]
    public async Task RunAsync_Prose_IsUnparseable()
    {
        this.process.Text = "I think yes.";

        HelperOutcome<ScriptedVerdict> outcome = await this.Build().RunAsync(this.helper, Settings, "x", TestContext.Current.CancellationToken);

        HelperOutcome<ScriptedVerdict>.Unparseable unparseable = Assert.IsType<HelperOutcome<ScriptedVerdict>.Unparseable>(outcome);
        Assert.Equal("no JSON object", unparseable.Problem);
        Assert.Equal("I think yes.", unparseable.Text);
    }

    /// <summary>A missing required property is Unparseable, not a quiet default.</summary>
    [Fact]
    public async Task RunAsync_MissingRequiredProperty_IsUnparseable()
    {
        this.process.Text = """{"why": "forgot the answer"}""";

        HelperOutcome<ScriptedVerdict> outcome = await this.Build().RunAsync(this.helper, Settings, "x", TestContext.Current.CancellationToken);

        Assert.IsType<HelperOutcome<ScriptedVerdict>.Unparseable>(outcome);
    }

    /// <summary>A Verdict the Helper's Validate refuses is Unparseable with its problem.</summary>
    [Fact]
    public async Task RunAsync_ValidateProblem_IsUnparseableWithProblem()
    {
        this.process.Text = """{"yes": true, "why": "bad"}""";

        HelperOutcome<ScriptedVerdict> outcome = await this.Build().RunAsync(this.helper, Settings, "x", TestContext.Current.CancellationToken);

        Assert.Equal("the reason was bad", Assert.IsType<HelperOutcome<ScriptedVerdict>.Unparseable>(outcome).Problem);
    }

    /// <summary>The text kept for the log is cut to 2,000 characters.</summary>
    [Fact]
    public async Task RunAsync_LongProse_KeepsTwoThousandCharacters()
    {
        this.process.Text = new string('a', 5000);

        HelperOutcome<ScriptedVerdict> outcome = await this.Build().RunAsync(this.helper, Settings, "x", TestContext.Current.CancellationToken);

        Assert.Equal(2000, Assert.IsType<HelperOutcome<ScriptedVerdict>.Unparseable>(outcome).Text.Length);
    }

    /// <summary>An Adapter id that is not configured fails before any process or any call is spent.</summary>
    [Fact]
    public async Task RunAsync_UnknownAdapter_FailsWithoutSpending()
    {
        HelperRunner runner = this.Build();

        HelperOutcome<ScriptedVerdict> outcome = await runner.RunAsync(this.helper, Settings with { Adapter = "nope" }, "x", TestContext.Current.CancellationToken);

        Assert.Equal("The Adapter 'nope' is not configured. Pick another on this card.", Assert.IsType<HelperOutcome<ScriptedVerdict>.Failed>(outcome).Reason);
        Assert.Equal(0, this.process.Calls);
    }

    /// <summary>An Adapter with no launch command fails as not installed, and starts nothing.</summary>
    [Fact]
    public async Task RunAsync_AdapterNotInstalled_Fails()
    {
        HelperOutcome<ScriptedVerdict> outcome = await this.Build(resolvable: false).RunAsync(this.helper, Settings, "x", TestContext.Current.CancellationToken);

        Assert.Equal("Claude isn't installed on this computer.", Assert.IsType<HelperOutcome<ScriptedVerdict>.Failed>(outcome).Reason);
        Assert.Equal(0, this.process.Calls);
    }

    /// <summary>A Model the session does not offer fails with the card's wording.</summary>
    [Fact]
    public async Task RunAsync_ModelNotOffered_Fails()
    {
        this.process.Handler = static (_, _) => Task.FromResult(new HelperTurn(string.Empty, ModelOffered: false, 0, null));

        HelperOutcome<ScriptedVerdict> outcome = await this.Build().RunAsync(this.helper, Settings with { Model = "gone-1" }, "x", TestContext.Current.CancellationToken);

        Assert.Equal("Claude doesn't offer the model gone-1. Pick another on this card.", Assert.IsType<HelperOutcome<ScriptedVerdict>.Failed>(outcome).Reason);
    }

    /// <summary>A process that throws is a Failed outcome carrying its message.</summary>
    [Fact]
    public async Task RunAsync_ProcessThrows_Fails()
    {
        this.process.Handler = static (_, _) => throw new IOException("adapter exited");

        HelperOutcome<ScriptedVerdict> outcome = await this.Build().RunAsync(this.helper, Settings, "x", TestContext.Current.CancellationToken);

        Assert.Equal("adapter exited", Assert.IsType<HelperOutcome<ScriptedVerdict>.Failed>(outcome).Reason);
    }

    /// <summary>A call that outlasts the Helper's timeout is cancelled and reported as timed out.</summary>
    [Fact]
    public async Task RunAsync_Timeout_CancelsAndReports()
    {
        TaskCompletionSource never = new();
        this.process.Hold = never.Task;
        HelperRunner runner = this.Build();

        Task<HelperOutcome<ScriptedVerdict>> run = runner.RunAsync(this.helper, Settings with { TimeoutSeconds = 30 }, "x", TestContext.Current.CancellationToken);
        while (this.process.Calls == 0)
        {
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        this.clock.Advance(TimeSpan.FromSeconds(31));

        Assert.Equal("Timed out after 30 s", Assert.IsType<HelperOutcome<ScriptedVerdict>.Failed>(await run).Reason);
    }

    /// <summary>The caller's own cancellation is rethrown, not reported as a timeout.</summary>
    [Fact]
    public async Task RunAsync_CallerCancels_Throws()
    {
        this.process.Hold = new TaskCompletionSource().Task;
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Task<HelperOutcome<ScriptedVerdict>> run = this.Build().RunAsync(this.helper, Settings, "x", cts.Token);
        while (this.process.Calls == 0)
        {
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    /// <summary>With the budget spent the outcome is OverBudget and no process starts.</summary>
    [Fact]
    public async Task RunAsync_OverBudget_StartsNoProcess()
    {
        HelperRunner runner = this.Build();
        CancellationToken ct = TestContext.Current.CancellationToken;
        HelperSettings oneCall = Settings with { DailyCap = 1 };
        Assert.IsType<HelperOutcome<ScriptedVerdict>.Ok>(await runner.RunAsync(this.helper, oneCall, "a", ct));

        HelperOutcome<ScriptedVerdict> second = await runner.RunAsync(this.helper, oneCall, "b", ct);

        Assert.IsType<HelperOutcome<ScriptedVerdict>.OverBudget>(second);
        Assert.Equal(1, this.process.Calls);
    }

    /// <summary>A call that fails after launching still counts against the budget.</summary>
    [Fact]
    public async Task RunAsync_FailedCall_StillCounts()
    {
        this.process.Handler = static (_, _) => throw new IOException("boom");
        HelperRunner runner = this.Build();
        CancellationToken ct = TestContext.Current.CancellationToken;
        HelperSettings oneCall = Settings with { DailyCap = 1 };

        Assert.IsType<HelperOutcome<ScriptedVerdict>.Failed>(await runner.RunAsync(this.helper, oneCall, "a", ct));

        Assert.IsType<HelperOutcome<ScriptedVerdict>.OverBudget>(await runner.RunAsync(this.helper, oneCall, "b", ct));
    }
}
