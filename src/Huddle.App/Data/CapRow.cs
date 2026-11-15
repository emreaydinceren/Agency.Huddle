namespace Agency.Huddle.App.Data;

/// <summary>How far one scope's spend has come against its cap (ADR-0087).</summary>
/// <param name="Scope">The scope key: <c>global</c>, <c>team:Team</c> or <c>project:Team/Project</c>.</param>
/// <param name="Cap">The cap, in USD, the row was started for.</param>
/// <param name="Baseline">What the counted Personas had spent, in USD, when counting started.</param>
/// <param name="Allowance">What the scope may spend since the baseline before Huddle asks; it starts at the cap and grows by one cap per Yes.</param>
/// <param name="Asked">Whether a question is open.</param>
/// <param name="Declined">Whether the Human said No.</param>
internal sealed record CapRow(string Scope, decimal Cap, decimal Baseline, decimal Allowance, bool Asked, bool Declined);
