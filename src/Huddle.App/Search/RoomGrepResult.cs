namespace Agency.Huddle.App.Search;

/// <summary>The outcome of one <see cref="RoomGrep"/> search.</summary>
/// <param name="Text">The Rooms section, starting with its heading, or empty when nothing matched.</param>
/// <param name="Refusal">A sentence that refuses the call, or null.</param>
/// <param name="Unreadable">Whether every Room in scope had an unreadable Transcript.</param>
internal sealed record RoomGrepResult(string Text, string? Refusal = null, bool Unreadable = false);
