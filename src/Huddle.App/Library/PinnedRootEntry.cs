namespace Agency.Huddle.App.Library;

/// <summary>A pinned root in the settings: display name and path.</summary>
/// <param name="Name">The display name of the pinned root.</param>
/// <param name="Path">The path to the pinned root, absolute or relative to DataDir.</param>
internal sealed record PinnedRootEntry(string Name, string Path);
