namespace Agency.Huddle.App.Components.Feed;

/// <summary>A day heading in the Feed: the day word in 600, then the date in Quiet ink (Feed spec §6.12).</summary>
/// <param name="Word">Today, Yesterday, a weekday, or the date alone for older days.</param>
/// <param name="Date">The date drawn after the word, or <see langword="null"/> when the word is already the date.</param>
internal sealed record FeedDayHeading(string Word, string? Date);
