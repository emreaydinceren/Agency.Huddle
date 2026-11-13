namespace Agency.Huddle.Seeder.Model;

/// <summary>
/// A <c>schedule.md</c> section Huddle cannot read, written as it stands so the Scheduled page lists it with its reason.
/// Seeder spec SEED-SC-19.
/// </summary>
/// <param name="Owner">The Agent whose file holds it, by Name, or <see langword="null"/> for the Human's.</param>
/// <param name="Raw">The section's text, from its <c>## </c> heading on.</param>
internal sealed record SeedJobProblem(string? Owner, string Raw)
{
    /// <summary>The section's heading without its <c>## </c>, whatever line endings the source file was checked out with.</summary>
    internal string Title => this.Raw.ReplaceLineEndings("\n").Split('\n')[0][3..].Trim();
}
