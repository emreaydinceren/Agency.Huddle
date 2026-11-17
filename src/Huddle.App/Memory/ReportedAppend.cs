namespace Agency.Huddle.App.Memory;

/// <summary>What <see cref="ReportedFile.Append"/> did.</summary>
/// <param name="Text">The new whole text, or <see langword="null"/> when it does not fit the cap even with every dated line gone.</param>
/// <param name="DroppedByAge">How many lines were older than the rule allows.</param>
/// <param name="DroppedForRoom">How many of the oldest dated lines were dropped to fit the cap.</param>
/// <param name="Size">The size in bytes the text has, or would have had.</param>
internal sealed record ReportedAppend(string? Text, int DroppedByAge, int DroppedForRoom, int Size);
