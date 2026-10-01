using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp.Fakes;

/// <summary>
/// A test double for <see cref="IModelCatalog"/>. Never launches a real adapter — the real
/// <see cref="ModelCatalogProbe"/> spawns a process, and no test may. <see cref="ProbeCount"/> and
/// <see cref="EffortProbeCount"/> are what let a test prove the /teammates page never probes on a
/// plain page load.
/// </summary>
public sealed class FakeModelCatalog : IModelCatalog
{
    public IReadOnlyList<AgentModelOption> Models { get; set; } = [];

    /// <summary>
    /// The catalog to hand back for a specific Adapter id, set per test as needed. A
    /// <see cref="GetAsync"/> call whose <c>adapterId</c> is present here returns this entry
    /// instead of <see cref="Models"/> — the per-Adapter counterpart of
    /// <see cref="EffortLevelsByModel"/>. Keyed ordinal: an Adapter id is an identifier, never text
    /// shown to a human, so culture-aware comparison would be wrong here (agents/CSharpPrinciples.md).
    /// </summary>
    public Dictionary<string, IReadOnlyList<AgentModelOption>> ModelsByAdapter { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Every Adapter id <see cref="GetAsync"/> or <see cref="GetEffortLevelsAsync"/> was asked
    /// about, in call order — including <c>null</c> for the installation's default Adapter. This is
    /// the hook a test uses to prove which Adapter a probe actually ran against, and that changing
    /// the Adapter in the card spawns a probe for the NEW one (docs/agencyteam/rules.md).
    /// </summary>
    public List<string?> AdaptersProbed { get; } = [];

    /// <summary>
    /// When <see langword="true"/>, <see cref="GetAsync"/> still counts the probe but never resolves
    /// its returned <see cref="ValueTask{TResult}"/> - the only way a bUnit-rendered component's
    /// "still loading" state can be observed at all, since a normally-completed
    /// <see cref="ValueTask{TResult}"/> is awaited synchronously and never gives the render pipeline a
    /// chance to paint the in-between state.
    /// </summary>
    public bool NeverCompletes { get; set; }

    /// <summary>
    /// When set, <see cref="GetAsync"/> returns <see cref="Models"/> only once this gate is completed -
    /// "answers, but asynchronously", which <see cref="NeverCompletes"/> cannot express and a plain
    /// <see cref="ValueTask.FromResult{TResult}(TResult)"/> cannot either, being awaited without ever
    /// yielding. That distinction is the whole point: a synchronously-answered catalog has already
    /// landed by the time a handler first yields, so the render gap issue #39 named - the model catalog
    /// arriving while the effort probe is still out - is unobservable without a gate to hold it open.
    /// </summary>
    public TaskCompletionSource? ModelsGate { get; set; }

    public int ProbeCount { get; private set; }

    /// <summary>The effort ladder to hand back for a specific model id, set per test as needed.</summary>
    public Dictionary<string, IReadOnlyList<AgentEffortOption>> EffortLevelsByModel { get; } = [];

    /// <summary>
    /// The effort ladder <see cref="GetEffortLevelsAsync"/> returns for any model not present in
    /// <see cref="EffortLevelsByModel"/> — including the default-model probe, whose key is
    /// <c>null</c>.
    /// </summary>
    public IReadOnlyList<AgentEffortOption> DefaultEffortLevels { get; set; } = [];

    /// <summary>The Effort-catalog counterpart of <see cref="NeverCompletes"/> - see its remarks.</summary>
    public bool EffortsNeverComplete { get; set; }

    public int EffortProbeCount { get; private set; }

    /// <summary>
    /// Every model id <see cref="GetEffortLevelsAsync"/> was asked about, in call order — including
    /// <c>null</c> for a default-model probe. This is the hook a future test uses to prove a
    /// re-probe after a model switch used the NEW model id rather than the old one.
    /// </summary>
    public List<string?> EffortProbedModels { get; } = [];

    public ValueTask<IReadOnlyList<AgentModelOption>> GetAsync(string? adapterId, CancellationToken cancellationToken)
    {
        this.ProbeCount++;
        this.AdaptersProbed.Add(adapterId);

        var models = adapterId is not null && this.ModelsByAdapter.TryGetValue(adapterId, out var forAdapter)
            ? forAdapter
            : this.Models;

        if (this.NeverCompletes)
        {
            return new ValueTask<IReadOnlyList<AgentModelOption>>(new TaskCompletionSource<IReadOnlyList<AgentModelOption>>().Task);
        }

        if (this.ModelsGate is not null)
        {
            return new ValueTask<IReadOnlyList<AgentModelOption>>(FakeModelCatalog.AfterAsync(this.ModelsGate, models));
        }

        return ValueTask.FromResult(models);
    }

    /// <summary>Hands back <paramref name="models"/> once <paramref name="gate"/> completes - see <see cref="ModelsGate"/>.</summary>
    /// <param name="gate">The gate to wait on before answering.</param>
    /// <param name="models">The catalog to return once the gate opens.</param>
    /// <returns>The catalog, no sooner than the gate's completion.</returns>
    private static async Task<IReadOnlyList<AgentModelOption>> AfterAsync(TaskCompletionSource gate, IReadOnlyList<AgentModelOption> models)
    {
        await gate.Task;
        return models;
    }

    public ValueTask<IReadOnlyList<AgentEffortOption>> GetEffortLevelsAsync(string? adapterId, string? model, CancellationToken cancellationToken)
    {
        this.EffortProbeCount++;
        this.EffortProbedModels.Add(model);
        this.AdaptersProbed.Add(adapterId);

        if (this.EffortsNeverComplete)
        {
            return new ValueTask<IReadOnlyList<AgentEffortOption>>(new TaskCompletionSource<IReadOnlyList<AgentEffortOption>>().Task);
        }

        var levels = model is not null && this.EffortLevelsByModel.TryGetValue(model, out var forModel)
            ? forModel
            : this.DefaultEffortLevels;

        return ValueTask.FromResult(levels);
    }

    /// <summary>
    /// The Work Modes <see cref="GetWorkModesAsync"/> hands back, already filtered the way the real catalog
    /// filters them: this fake never applies the hidden-modes policy, so a test sets exactly what the card
    /// should be offered.
    /// </summary>
    public IReadOnlyList<AgentModeOption> WorkModes { get; set; } = [];

    /// <summary>The Work Mode counterpart of <see cref="NeverCompletes"/> - see its remarks.</summary>
    public bool WorkModesNeverComplete { get; set; }

    /// <summary>How many times <see cref="GetWorkModesAsync"/> was called.</summary>
    public int WorkModeProbeCount { get; private set; }

    /// <summary>Every (Adapter id, model id) pair <see cref="GetWorkModesAsync"/> was asked about, in call order.</summary>
    public List<(string? AdapterId, string? Model)> WorkModesProbed { get; } = [];

    public ValueTask<IReadOnlyList<AgentModeOption>> GetWorkModesAsync(string? adapterId, string? model, CancellationToken cancellationToken)
    {
        this.WorkModeProbeCount++;
        this.WorkModesProbed.Add((adapterId, model));
        this.AdaptersProbed.Add(adapterId);

        if (this.WorkModesNeverComplete)
        {
            return new ValueTask<IReadOnlyList<AgentModeOption>>(new TaskCompletionSource<IReadOnlyList<AgentModeOption>>().Task);
        }

        return ValueTask.FromResult(this.WorkModes);
    }
}