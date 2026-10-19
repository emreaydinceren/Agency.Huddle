namespace Agency.Huddle.App.Components.Shared;

/// <summary>One folder offered by <see cref="FolderBrowser"/>.</summary>
/// <param name="Name">The label: the folder's name, or a drive's root such as <c>C:\</c>.</param>
/// <param name="Path">The full path.</param>
internal sealed record BrowsedFolder(string Name, string Path);
