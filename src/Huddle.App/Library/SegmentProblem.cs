namespace Agency.Huddle.App.Library;

/// <summary>A single problem found in a path segment by <see cref="LibrarySegments.Check(string)"/>.</summary>
/// <param name="Kind">The kind of problem found.</param>
/// <param name="Character">The offending character, when <paramref name="Kind"/> is <see cref="SegmentProblemKind.InvalidCharacter"/>.</param>
/// <param name="DeviceName">The reserved device name, when <paramref name="Kind"/> is <see cref="SegmentProblemKind.ReservedDeviceName"/>.</param>
internal sealed record SegmentProblem(SegmentProblemKind Kind, char Character = '\0', string? DeviceName = null);
