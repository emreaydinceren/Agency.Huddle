namespace Agency.Huddle.Seeder.Model;

/// <summary>A chat message in a Room transcript.</summary>
/// <param name="Sender">The Human's name or an Agent's Name.</param>
/// <param name="DaysAgo">Whole days before the run date.</param>
/// <param name="Time">The time of day in UTC, <c>HH:mm</c>.</param>
/// <param name="Text">The Markdown text.</param>
internal sealed record SeedMessage(string Sender, int DaysAgo, string Time, string Text);
