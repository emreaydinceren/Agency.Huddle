using MudBlazor;

namespace Agency.Huddle.App.Components.Shared;

/// <summary>
/// One icon per action, as design-system.md "Icons" gives them. A menu or button takes its icon from
/// here, so the same action never wears two icons (UX-083, UX-084, UX-R26).
/// </summary>
internal static class ActionIcons
{
    /// <summary>Add or New.</summary>
    public const string Add = Icons.Material.Filled.Add;

    /// <summary>Close a dialog, panel or Tab, or remove an item from a set the Human is building.</summary>
    public const string Close = Icons.Material.Filled.Close;

    /// <summary>Delete permanently.</summary>
    public const string Delete = Icons.Material.Filled.Delete;

    /// <summary>Remove a member from a Team.</summary>
    public const string RemoveMember = Icons.Material.Filled.PersonRemove;

    /// <summary>Remove from a list where the thing itself survives.</summary>
    public const string RemoveFromList = Icons.Material.Filled.RemoveCircleOutline;

    /// <summary>Dismiss a notice or a Needs You row.</summary>
    public const string Dismiss = Icons.Material.Filled.Check;

    /// <summary>Edit or Rename.</summary>
    public const string Edit = Icons.Material.Filled.Edit;

    /// <summary>Copy text, an id or a path.</summary>
    public const string Copy = Icons.Material.Filled.ContentCopy;

    /// <summary>Copy a link.</summary>
    public const string CopyLink = Icons.Material.Filled.Link;

    /// <summary>Duplicate an entity.</summary>
    public const string Duplicate = Icons.Material.Filled.FileCopy;

    /// <summary>More actions.</summary>
    public const string MoreActions = Icons.Material.Filled.MoreVert;

    /// <summary>Search.</summary>
    public const string Search = Icons.Material.Filled.Search;

    /// <summary>
    /// New note: the Material Symbols "note_add" glyph. It is drawn on a 960-unit grid that starts at
    /// y = -960, so the group scales it onto MudBlazor's 24-unit icon box.
    /// </summary>
    public const string NewNote = """<g transform="scale(0.025) translate(0,960)"><path d="M440-240h80v-120h120v-80H520v-120h-80v120H320v80h120v120ZM240-80q-33 0-56.5-23.5T160-160v-640q0-33 23.5-56.5T240-880h320l240 240v480q0 33-23.5 56.5T720-80H240Zm280-520v-200H240v640h480v-440H520ZM240-800v200-200 640-640Z"/></g>""";

    /// <summary>New folder.</summary>
    public const string NewFolder = Icons.Material.Outlined.CreateNewFolder;
}
