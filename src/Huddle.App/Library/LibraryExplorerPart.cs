namespace Agency.Huddle.App.Library;

/// <summary>Which half of the Library explorer a host mounts. The tree and the document normally share one component; the Sidebar and the Library page each take one half and meet through <see cref="LibraryBrowserBridge"/>.</summary>
public enum LibraryExplorerPart
{
    /// <summary>The tree and the document, split by a divider: the Team and Project Files tabs.</summary>
    Both,

    /// <summary>Only the tree (search, New note, New folder and the folders and files): the Library view of the Sidebar. Opening a file sends it to the Library page.</summary>
    TreeOnly,

    /// <summary>Only the open document: the Library page, which the Sidebar's tree drives.</summary>
    DocumentOnly,
}
