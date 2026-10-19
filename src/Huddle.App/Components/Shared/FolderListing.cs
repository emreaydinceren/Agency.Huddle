namespace Agency.Huddle.App.Components.Shared;

/// <summary>What <see cref="FolderBrowser.List"/> found at one place.</summary>
/// <param name="Current">The folder listed, or <see langword="null"/> for the top level (the drives, or <c>/</c>).</param>
/// <param name="Parent">The folder one level up, or <see langword="null"/> when there is none (a drive root goes up to the top level, shown by <see cref="Current"/> being null).</param>
/// <param name="Folders">The sub-folders, ordered by name; hidden and system folders are left out.</param>
/// <param name="Truncated">Whether more folders exist than <see cref="FolderBrowser.MaxFolders"/>.</param>
/// <param name="Error">Why the folder could not be read, or <see langword="null"/>.</param>
internal sealed record FolderListing(string? Current, string? Parent, IReadOnlyList<BrowsedFolder> Folders, bool Truncated, string? Error)
{
    /// <summary>Whether a folder is open that can be chosen; the top level can only be walked into.</summary>
    public bool CanSelect => this.Current is not null && this.Error is null;
}
