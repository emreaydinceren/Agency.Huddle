using System.Diagnostics;

namespace Agency.Huddle.App.Acp.Sessions.TurnHooks;

/// <summary>Runs the applicable hooks around a Turn, in the order of the list it is given.</summary>
internal sealed class TurnHookPipeline(IReadOnlyList<ITurnHook> hooks, TurnHookScope scope, ILogger logger)
{
    /// <summary>The length above which a hook's block is logged as a Warning. The block is never cut.</summary>
    private const int WarnChars = 10_000;

    private readonly List<ITurnHook> applicable = [.. hooks.Where(hook => hook.AppliesTo(scope))];

    /// <summary>Gets the names of the applicable hooks, in the order they run.</summary>
    internal IReadOnlyList<string> HookNames => [.. this.applicable.Select(hook => hook.Name)];

    /// <summary>
    /// The one place that knows the production order of the hooks, by name: the Team rename notice first because it says where files
    /// moved, then AGENTS.md because a rule change governs how the Agent reads everything after it, then the Huddle-wide Memory hook,
    /// then File Changes. A new hook is one class and one entry here.
    /// </summary>
    internal static readonly List<string> ProductionOrder =
    [
        TeamRenamedTurnHook.HookName,
        AgentsMdTurnHook.HookName,
        HuddleMemoryTurnHook.HookName,
        FileChangesTurnHook.HookName,
    ];

    /// <summary>Builds the production pipeline for one Persona from the registered hooks, sorted into the production order.</summary>
    /// <param name="scope">Who the pipeline serves.</param>
    /// <param name="hooks">The registered hooks, in any order; one that is absent from the production order is skipped.</param>
    /// <param name="logger">Receives the pipeline's warnings and timings.</param>
    /// <returns>The pipeline; its applicable list is empty when nothing applies to <paramref name="scope"/>.</returns>
    /// <exception cref="InvalidOperationException">A hook's name is not in the production order.</exception>
    internal static TurnHookPipeline ForPersona(TurnHookScope scope, IEnumerable<ITurnHook> hooks, ILogger logger)
    {
        return new TurnHookPipeline(InProductionOrder(hooks), scope, logger);
    }

    /// <summary>Builds the pipeline a goldfish possession gets: the same hooks under a clean scope, so none applies.</summary>
    /// <returns>The clean pipeline.</returns>
    internal TurnHookPipeline ForClean()
    {
        return new TurnHookPipeline(hooks, scope with { Clean = true }, logger);
    }

    /// <summary>Sorts the hooks into the production order.</summary>
    private static List<ITurnHook> InProductionOrder(IEnumerable<ITurnHook> hooks)
    {
        List<ITurnHook> sorted = [];
        foreach (ITurnHook hook in hooks)
        {
            if (!ProductionOrder.Contains(hook.Name, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Turn hook '{hook.Name}' is not in the production order; add its name to TurnHookPipeline.ProductionOrder.");
            }

            sorted.Add(hook);
        }

        return [.. sorted.OrderBy(hook => ProductionOrder.IndexOf(hook.Name))];
    }

    /// <summary>Runs every applicable hook's Before point in list order.</summary>
    /// <param name="roomId">The Room being prompted.</param>
    /// <param name="state">The session's hook state, keyed by hook name.</param>
    /// <param name="runToken">The run's token.</param>
    /// <param name="stopToken">The Stop token.</param>
    /// <returns>The blocks to add and each hook's addition.</returns>
    public async Task<TurnHookBeforeResult> BeforeTurnAsync(
        string roomId,
        IReadOnlyDictionary<string, string?> state,
        CancellationToken runToken,
        CancellationToken stopToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        List<string> blocks = [];
        Dictionary<string, TurnAddition> additions = new(StringComparer.Ordinal);
        foreach (ITurnHook hook in this.applicable)
        {
            runToken.ThrowIfCancellationRequested();
            if (stopToken.IsCancellationRequested)
            {
                break;
            }

            _ = state.TryGetValue(hook.Name, out string? current);
            TurnAddition? addition;
            try
            {
                addition = await this
                    .RunBeforeAsync(hook, new TurnStarting(scope, roomId, current), runToken, stopToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Turn hook {HookName} failed before the Turn; its state is unchanged.", hook.Name);
                continue;
            }
            catch (OperationCanceledException) when (!runToken.IsCancellationRequested && stopToken.IsCancellationRequested)
            {
                break;
            }

            if (addition is null)
            {
                continue;
            }

            additions[hook.Name] = addition;
            if (!string.IsNullOrEmpty(addition.Text))
            {
                blocks.Add(addition.Text);
            }
        }

        return new TurnHookBeforeResult(blocks, additions);
    }

    /// <summary>Runs one Before point on a token linked from both tokens, and logs its name, time and characters.</summary>
    private async Task<TurnAddition?> RunBeforeAsync(
        ITurnHook hook,
        TurnStarting turn,
        CancellationToken runToken,
        CancellationToken stopToken)
    {
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(runToken, stopToken);
        long started = Stopwatch.GetTimestamp();
        TurnAddition? addition = await hook.BeforeTurnAsync(turn, linked.Token).ConfigureAwait(false);
        int chars = addition?.Text?.Length ?? 0;
#pragma warning disable HAA0601 // Boxing a value-type log argument is the cost of ILogger params object[]; accepted together with HAA0101.
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "Turn hook {HookName} ran in {ElapsedMs} ms and added {Chars} characters.",
                hook.Name,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                chars);
        }

        if (chars > WarnChars)
        {
            logger.LogWarning(
                "Turn hook {HookName} added {Chars} characters, over {Limit}; the block is kept whole.",
                hook.Name,
                chars,
                WarnChars);
        }
#pragma warning restore HAA0601

        return addition;
    }

    /// <summary>Runs the After point of each hook that returned an addition, storing the state it returns.</summary>
    /// <param name="roomId">The Room that was prompted.</param>
    /// <param name="state">The session's hook state, keyed by hook name.</param>
    /// <param name="before">What the Before points produced.</param>
    /// <param name="touched">The paths touched during the Turn.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when every After point has run.</returns>
    public async Task AfterTurnAsync(
        string roomId,
        Dictionary<string, string?> state,
        TurnHookBeforeResult before,
        IReadOnlyCollection<string> touched,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(before);
        foreach (ITurnHook hook in this.applicable)
        {
            if (!before.Additions.TryGetValue(hook.Name, out TurnAddition? addition))
            {
                continue;
            }

            _ = state.TryGetValue(hook.Name, out string? current);
            try
            {
                state[hook.Name] = await hook
                    .AfterTurnAsync(new TurnEnded(scope, roomId, current, touched), addition, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Turn hook {HookName} failed after the Turn; its state is unchanged.", hook.Name);
            }
        }
    }

    /// <summary>Runs every applicable hook's Opening point, storing the state it returns.</summary>
    /// <param name="reason">Why the session is opening.</param>
    /// <param name="state">The session's hook state, keyed by hook name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when every Opening point has run.</returns>
    public async Task OnSessionOpeningAsync(
        SessionOpenReason reason,
        Dictionary<string, string?> state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        foreach (ITurnHook hook in this.applicable)
        {
            string? stored = null;
            if (reason != SessionOpenReason.Fresh)
            {
                _ = state.TryGetValue(hook.Name, out stored);
            }

            try
            {
                state[hook.Name] = await hook
                    .OnSessionOpeningAsync(new SessionOpening(scope, reason, stored), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Turn hook {HookName} failed while the session opened; its state is unchanged.", hook.Name);
            }
        }
    }
}
