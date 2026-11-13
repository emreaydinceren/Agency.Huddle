namespace Agency.Huddle.Seeder.Model;

/// <summary>One past run of a <see cref="SeedJob"/>: Huddle's post at that time, then the replies it drew. Seeder spec SEED-SC-19.</summary>
/// <param name="DaysAgo">Whole days before the run date.</param>
/// <param name="Time">The time of day in UTC, <c>HH:mm</c>.</param>
/// <param name="Replies">What the Agent, or the Human, answered, oldest first.</param>
internal sealed record SeedJobRun(int DaysAgo, string Time, IReadOnlyList<SeedMessage> Replies);
