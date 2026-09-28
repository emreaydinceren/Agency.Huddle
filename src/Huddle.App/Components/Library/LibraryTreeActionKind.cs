namespace Agency.Huddle.App.Components.Library;

/// <summary>The action a <see cref="LibraryTree"/> row's context menu (or "..." button) can raise (Task 12.1).</summary>
public enum LibraryTreeActionKind
{
    /// <summary>Create a new note beside the node the menu was opened on.</summary>
    NewNote,

    /// <summary>Create a new folder beside the node the menu was opened on.</summary>
    NewFolder,

    /// <summary>Create a new Project folder inside a Team folder.</summary>
    NewProject,

    /// <summary>Rename the node.</summary>
    Rename,

    /// <summary>Move the node.</summary>
    Move,

    /// <summary>Copy the node's path to the clipboard.</summary>
    CopyPath,

    /// <summary>Open the node in the operating system's default application.</summary>
    OpenInDefaultApp,

    /// <summary>Delete the node.</summary>
    Delete,
}
