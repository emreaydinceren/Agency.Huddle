using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp.Fakes;

/// <summary>
/// A test double for <see cref="IAdapterProbeRunner"/>. Stands in for the real process spawn so
/// <see cref="ModelCatalogProbe"/>'s caching, keying and gating logic is testable without launching
/// a real adapter — docs/engineering/rules.md row 35 ("No test may reach the real
/// <see cref="ModelCatalogProbe"/>") forbids exactly that, and this is the seam
/// <see cref="ModelCatalogProbe"/> now calls instead of spawning directly.
/// </summary>
internal sealed class FakeAdapterProbeRunner : IAdapterProbeRunner
{
    /// <summary>Every call this runner received, in order.</summary>
    internal List<(AgentProcessOptions ProcessOptions, string ProbeCwd, string? Model)> Calls { get; } = [];

    /// <summary>
    /// Answers every call by default: an empty, successful outcome. Reassign per test to return a
    /// specific catalog, to answer differently depending on <see cref="Calls"/>' current count, or
    /// to throw one of the exceptions <see cref="ModelCatalogProbe"/> already catches (for example
    /// <see cref="IOException"/>), simulating a failed probe.
    /// </summary>
    internal Func<AgentProcessOptions, string, string?, AdapterProbeOutcome> Handler { get; set; } =
        static (_, _, _) => new AdapterProbeOutcome([], []);

    /// <inheritdoc/>
    public Task<AdapterProbeOutcome> RunAsync(AgentProcessOptions processOptions, string probeCwd, string? model, CancellationToken cancellationToken)
    {
        this.Calls.Add((processOptions, probeCwd, model));
        return Task.FromResult(this.Handler(processOptions, probeCwd, model));
    }
}
