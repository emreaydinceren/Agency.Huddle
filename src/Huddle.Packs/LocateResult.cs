namespace Agency.Huddle.Packs;

/// <summary>The outcome of finding the data: the location, or the exit-2 message.</summary>
/// <param name="Location">The location, or null.</param>
/// <param name="Error">The message for standard error, or null.</param>
internal sealed record LocateResult(DataLocation? Location, string? Error);
