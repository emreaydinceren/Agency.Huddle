namespace Agency.Huddle.Seeder.Model;

/// <summary>One extra <c>event</c> on a Task's timeline, beyond the ones derived from its status.</summary>
/// <param name="Actor">Who did it.</param>
/// <param name="Summary">The app-style summary, such as <c>priority: Medium → High</c>.</param>
internal sealed record SeedTimelineEvent(string Actor, string Summary);
