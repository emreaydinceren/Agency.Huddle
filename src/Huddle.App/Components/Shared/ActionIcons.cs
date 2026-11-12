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

    /// <summary>
    /// A Project: the Material Symbols Outlined "construction" glyph (the crossed hammer and wrench)
    /// scaled onto a landscape sheet outline. The sheet is a 2.5-unit stroke in the current colour and the
    /// tools are solid, on MudBlazor's 24-unit icon box.
    /// </summary>
    public const string Project = """<rect x="1.25" y="2.5" width="21.5" height="19" rx="2" fill="none" stroke="currentColor" stroke-width="2.5"/><g transform="translate(12 12) scale(0.75) translate(-11.5 -12.5)"><rect height="8.48" transform="matrix(0.7071 -0.7071 0.7071 0.7071 -6.8717 17.6255)" width="3" x="16.34" y="12.87"/><path d="M17.5,10c1.93,0,3.5-1.57,3.5-3.5c0-0.58-0.16-1.12-0.41-1.6l-2.7,2.7L16.4,6.11l2.7-2.7C18.62,3.16,18.08,3,17.5,3 C15.57,3,14,4.57,14,6.5c0,0.41,0.08,0.8,0.21,1.16l-1.85,1.85l-1.78-1.78l0.71-0.71L9.88,5.61L12,3.49 c-1.17-1.17-3.07-1.17-4.24,0L4.22,7.03l1.41,1.41H2.81L2.1,9.15l3.54,3.54l0.71-0.71V9.15l1.41,1.41l0.71-0.71l1.78,1.78 l-7.41,7.41l2.12,2.12L16.34,9.79C16.7,9.92,17.09,10,17.5,10z"/></g>""";

    /// <summary>
    /// A chat: two overlapping speech bubbles, drawn as outlines on MudBlazor's 24-unit icon box. It stands
    /// for the Chats view, a Chat Tab and a Room row.
    /// </summary>
    public const string Chat = """<path stroke="currentColor" stroke-width="0.8" stroke-linejoin="round" d="M5.7898124,15.0400458 C5.92222516,15.0022135 6.06437918,15.0206689 6.18274191,15.0910584 C7.17821446,15.6830589 8.31529906,16 9.5,16 C13.0898509,16 16,13.0898509 16,9.5 C16,5.91014913 13.0898509,3 9.5,3 C5.91014913,3 3,5.91014913 3,9.5 C3,10.6847009 3.31694108,11.8217855 3.90894164,12.8172581 C3.97933113,12.9356208 3.99778647,13.0777748 3.95995425,13.2101876 L3.22801099,15.771989 L5.7898124,15.0400458 Z M16.6787004,7.32146597 C19.8088582,8.2695726 22,11.1682874 22,14.5 C22,15.791292 21.6727837,17.0357461 21.0596438,18.1387259 L21.980762,21.3626394 C22.0885265,21.7398154 21.7398154,22.0885265 21.3626394,21.980762 L18.1387259,21.0596438 C17.0357461,21.6727837 15.791292,22 14.5,22 C11.1714936,22 8.27116719,19.8126453 7.32145812,16.6786102 C6.81438749,16.5249459 6.32531045,16.3175988 5.86127413,16.0596438 L2.63736056,16.980762 C2.26018462,17.0885265 1.91147347,16.7398154 2.01923803,16.3626394 L2.94035619,13.1387259 C2.32721633,12.0357461 2,10.791292 2,9.5 C2,5.35786438 5.35786438,2 9.5,2 C12.8844665,2 15.7453349,4.24179021 16.6787004,7.32146597 L16.6787004,7.32146597 Z M16.9296994,8.46886327 C16.9760497,8.80594809 17,9.1501771 17,9.5 C17,13.6421356 13.6421356,17 9.5,17 C9.15228938,17 8.8079749,16.9762741 8.46890428,16.9296286 C9.44418788,19.352582 11.8110214,21 14.5,21 C15.6847009,21 16.8217855,20.6830589 17.8172581,20.0910584 C17.9356208,20.0206689 18.0777748,20.0022135 18.2101876,20.0400458 L20.771989,20.771989 L20.0400458,18.2101876 C20.0022135,18.0777748 20.0206689,17.9356208 20.0910584,17.8172581 C20.6830589,16.8217855 21,15.6847009 21,14.5 C21,11.8086534 19.3499538,9.44299439 16.9296994,8.46886327 L16.9296994,8.46886327 Z"/>""";

    /// <summary>New folder.</summary>
    public const string NewFolder = Icons.Material.Outlined.CreateNewFolder;
}
