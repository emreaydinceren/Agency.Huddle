namespace Agency.Huddle.App.Teams;

/// <summary>An open question: a scope has spent its cap running inside the Team, and the Human may let it spend one more cap (ADR-0087).</summary>
/// <param name="Scope">The scope key, which <see cref="NeedsYouCaps.Grant"/> and <see cref="NeedsYouCaps.Decline"/> take.</param>
/// <param name="ScopeName">What the Human calls the scope: <c>Huddle</c>, the Team's name, or <c>Team / Project</c>.</param>
/// <param name="Team">The Team's name; <see langword="null"/> for the global scope.</param>
/// <param name="Project">The Project's name; <see langword="null"/> unless the scope is a Project.</param>
/// <param name="Cap">The cap in USD, which is also what a Yes adds.</param>
/// <param name="Spent">What the scope has spent in USD since counting started.</param>
internal sealed record CapAsk(string Scope, string ScopeName, string? Team, string? Project, decimal Cap, decimal Spent);
