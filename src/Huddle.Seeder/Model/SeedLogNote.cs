namespace Agency.Huddle.Seeder.Model;

/// <summary>One extra Change log line for a Task, beyond the ones derived from its status.</summary>
/// <param name="Actor">Who did it.</param>
/// <param name="Summary">The app-style summary, such as <c>priority: Medium → High</c>.</param>
internal sealed record SeedLogNote(string Actor, string Summary);
