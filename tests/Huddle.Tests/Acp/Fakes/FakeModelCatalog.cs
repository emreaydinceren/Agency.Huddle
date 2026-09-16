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

        return ValueTask.FromResult(this.Models);
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