using System.Globalization;
using System.Text.Json;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Prompts;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Helpers;

/// <summary>
/// Does everything one Helper call needs that can be tested without a process: the budget, the adapter, the timeout and
/// the parse (Helpers Spec §6.2). It never throws for a failed call; every failure is a <see cref="HelperOutcome{TVerdict}"/>.
/// </summary>
/// <param name="processRunner">The seam that spawns the adapter.</param>
/// <param name="adapters">Resolves the Adapter id to a launch profile.</param>
/// <param name="prompts">Renders the Helper's prompt keys.</param>
/// <param name="budget">Takes the call before the process starts.</param>
/// <param name="options">The bound options, for the data directory.</param>
/// <param name="logger">Records each failure.</param>
/// <param name="timeProvider">The clock for the timeout; <see cref="TimeProvider.System"/> when <see langword="null"/>.</param>
internal sealed class HelperRunner(
    IHelperProcessRunner processRunner,
    AdapterCatalog adapters,
    IPromptSource prompts,
    HelperBudget budget,
    IOptions<TeamOptions> options,
    ILogger<HelperRunner> logger,
    TimeProvider? timeProvider = null)
{
    /// <summary>How much of an unparseable answer is kept for the log.</summary>
    private const int MaxKeptText = 2000;

    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    /// <summary>Runs one Helper call: adapter, budget, timeout, parse.</summary>
    /// <typeparam name="TInput">The Helper's input.</typeparam>
    /// <typeparam name="TVerdict">The Helper's Verdict.</typeparam>
    /// <param name="helper">The Helper.</param>
    /// <param name="settings">Its effective settings for this call.</param>
    /// <param name="input">What to judge.</param>
    /// <param name="cancellationToken">Cancels the call; the caller's cancellation is rethrown, not reported as a timeout.</param>
    public async Task<HelperOutcome<TVerdict>> RunAsync<TInput, TVerdict>(
        IHelperAgent<TInput, TVerdict> helper,
        HelperSettings settings,
        TInput input,
        CancellationToken cancellationToken)
        where TVerdict : class
    {
        ArgumentNullException.ThrowIfNull(helper);
        ArgumentNullException.ThrowIfNull(settings);

        AdapterProfile? profile = settings.Adapter is null ? adapters.Default : adapters.Find(settings.Adapter);
        if (profile is null)
        {
            return new HelperOutcome<TVerdict>.Failed($"The Adapter '{settings.Adapter}' is not configured. Pick another on this card.");
        }

        string workDir = Path.Combine(Path.GetFullPath(options.Value.DataDir), "helpers", "work");
        AgentProcessOptions? processOptions = AgentProcessOptionsFactory.TryCreate(profile, workDir, AppContext.BaseDirectory);
        if (processOptions is null)
        {
            return new HelperOutcome<TVerdict>.Failed($"{profile.DisplayName} isn't installed on this computer.");
        }

        HelperSessionRequest request = new(
            workDir,
            settings.Model,
            settings.Effort,
            prompts.Render(helper.PromptKeys[0], new Dictionary<string, string>(StringComparer.Ordinal)),
            prompts.Render(helper.PromptKeys[1], helper.PromptValues(input)),
            HelperSessionMeta.For(profile));

        if (!budget.TryReserve(helper.Id, settings.DailyCap))
        {
            return new HelperOutcome<TVerdict>.OverBudget();
        }

        ProcessRun run = await this.RunProcessAsync(helper.Id, processOptions, request, settings.TimeoutSeconds, cancellationToken).ConfigureAwait(false);
        if (run is ProcessRun.Finished finished)
        {
            return Interpret(helper, profile, settings, finished.Turn);
        }

        return new HelperOutcome<TVerdict>.Failed(((ProcessRun.Broke)run).Reason);
    }

    /// <summary>Runs the process under the Helper's timeout; a timeout or a failure is a <see cref="ProcessRun.Broke"/> that says why.</summary>
    private async Task<ProcessRun> RunProcessAsync(HelperId id, AgentProcessOptions processOptions, HelperSessionRequest request, int timeoutSeconds, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(timeoutSeconds), this.clock);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            HelperTurn turn = await processRunner.RunAsync(processOptions, request, linked.Token).ConfigureAwait(false);
            return new ProcessRun.Finished(turn);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Helper {HelperId} timed out.", id.Value);
            return new ProcessRun.Broke(string.Create(CultureInfo.InvariantCulture, $"Timed out after {timeoutSeconds} s"));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The adapter process can fail in many ways (launch, protocol, exit); each is a failed call, never a crash of the dispatcher.
            logger.LogWarning(ex, "Helper {HelperId} failed to run.", id.Value);
            return new ProcessRun.Broke(ex.Message);
        }
    }

    /// <summary>How a process run ended: with a Turn, or with the reason it did not.</summary>
    private abstract record ProcessRun
    {
        /// <summary>The adapter finished the Turn.</summary>
        /// <param name="Turn">What it produced.</param>
        public sealed record Finished(HelperTurn Turn) : ProcessRun;

        /// <summary>The call timed out or the process failed.</summary>
        /// <param name="Reason">Why, in words for the card.</param>
        public sealed record Broke(string Reason) : ProcessRun;
    }

    /// <summary>Turns a finished Turn into an outcome: the Model check, then the JSON.</summary>
    private static HelperOutcome<TVerdict> Interpret<TInput, TVerdict>(IHelperAgent<TInput, TVerdict> helper, AdapterProfile profile, HelperSettings settings, HelperTurn turn)
        where TVerdict : class
    {
        if (!turn.ModelOffered)
        {
            return new HelperOutcome<TVerdict>.Failed($"{profile.DisplayName} doesn't offer the model {settings.Model}. Pick another on this card.");
        }

        string text = turn.Text;
        int start = text.IndexOf('{', StringComparison.Ordinal);
        int end = text.LastIndexOf('}');
        if (start < 0 || end < start)
        {
            return Unparseable<TVerdict>(text, "no JSON object");
        }

        TVerdict? verdict;
        try
        {
            verdict = JsonSerializer.Deserialize(text.AsSpan(start, end - start + 1), helper.VerdictType);
        }
        catch (JsonException ex)
        {
            return Unparseable<TVerdict>(text, ex.Message);
        }

        if (verdict is null)
        {
            return Unparseable<TVerdict>(text, "no JSON object");
        }

        string? problem = helper.Validate(verdict);
        return problem is null ? new HelperOutcome<TVerdict>.Ok(verdict) : Unparseable<TVerdict>(text, problem);
    }

    /// <summary>An unparseable outcome with the text cut for the log.</summary>
    private static HelperOutcome<TVerdict> Unparseable<TVerdict>(string text, string problem)
        where TVerdict : class =>
        new HelperOutcome<TVerdict>.Unparseable(text.Length > MaxKeptText ? text[..MaxKeptText] : text, problem);
}
