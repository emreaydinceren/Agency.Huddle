using Microsoft.CodeAnalysis;

namespace Agency.Huddle.Analyzers;

/// <summary>
/// The descriptors of this repo's own rules. Each is a warning, which <c>TreatWarningsAsErrors</c> turns into a failed build,
/// so a deliberate exception can still take a scoped <c>#pragma</c> with its reason.
/// </summary>
internal static class HuddleDiagnostics
{
    private const string Reliability = "Reliability";

    /// <summary>HUD0001: an event raised with <c>?.Invoke</c>, <c>.Invoke</c> or a direct call instead of <c>EventRaise.Isolated</c>.</summary>
    internal static readonly DiagnosticDescriptor EventRaisedDirectly = new(
        id: "HUD0001",
        title: "Raise an event through EventRaise.Isolated",
        messageFormat: "Event '{0}' is raised directly; raise it through EventRaise.Isolated so a subscriber that throws neither skips the others nor fails the raiser",
        category: Reliability,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A raised event runs every subscriber in turn. Called directly, the first subscriber that throws skips the rest and "
            + "propagates into the raiser, after the raiser's state change, so a successful save looks failed. EventRaise.Isolated "
            + "runs each subscriber in its own try and reports the failure instead.");

    /// <summary>HUD0002: a hosted service holding a <c>CancellationTokenSource</c> field instead of a <c>StopSignal</c>.</summary>
    internal static readonly DiagnosticDescriptor HostedServiceTokenSource = new(
        id: "HUD0002",
        title: "A hosted service keeps its lifetime in a StopSignal",
        messageFormat: "Hosted service '{0}' holds CancellationTokenSource '{1}'; use StopSignal, because the host can stop and dispose it on two threads at once",
        category: Reliability,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Under WebApplicationFactory the factory stops the host on one thread while Program's RunAsync disposes it on "
            + "another, so a token source cancelled in StopAsync can already be disposed and throw ObjectDisposedException. "
            + "StopSignal serialises Stop against Dispose and keeps its token readable after disposal. A token source scoped to "
            + "one method (a using local) is not affected.");
}
