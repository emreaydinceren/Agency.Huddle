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

    public ValueTask<IReadOnlyList<AgentModelOption>> GetAsync(CancellationToken cancellationToken)
    {
        this.ProbeCount++;

        if (this.NeverCompletes)
        {
            return new ValueTask<IReadOnlyList<AgentModelOption>>(new TaskCompletionSource<IReadOnlyList<AgentModelOption>>().Task);
        }

        if (this.ModelsGate is not null)
        {
            return new ValueTask<IReadOnlyList<AgentModelOption>>(FakeModelCatalog.AfterAsync(this.ModelsGate, this.Models));
        }

        return ValueTask.FromResult(this.Models);
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

    public ValueTask<IReadOnlyList<AgentEffortOption>> GetEffortLevelsAsync(string? model, CancellationToken cancellationToken)
    {
        this.EffortProbeCount++;
        this.EffortProbedModels.Add(model);

        if (this.EffortsNeverComplete)
        {
            return new ValueTask<IReadOnlyList<AgentEffortOption>>(new TaskCompletionSource<IReadOnlyList<AgentEffortOption>>().Task);
        }

        var levels = model is not null && this.EffortLevelsByModel.TryGetValue(model, out var forModel)
            ? forModel
            : this.DefaultEffortLevels;

        return ValueTask.FromResult(levels);
    }
}