namespace Agency.Huddle.App.Library;

/// <summary>The outcome of <see cref="WikiLinkResolver.Resolve(IReadOnlyList{string}, string, string)"/>.</summary>
/// <param name="Path">The resolved root-relative note path, or <see langword="null"/> when nothing matches.</param>
/// <param name="IsAmbiguous">Whether several notes tied at the shortest distance from the linking note.</param>
internal sealed record WikiLinkResolution(string? Path, bool IsAmbiguous);
