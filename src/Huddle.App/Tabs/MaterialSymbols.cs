namespace Agency.Huddle.App.Tabs;

/// <summary>
/// Material Symbols that MudBlazor's classic Material Icons set does not ship, rescaled from Google's
/// 960-unit grid to the 24dp grid a <c>MudIcon</c> expects, so they render alongside <c>Icons.Material.Filled</c>.
/// </summary>
internal static class MaterialSymbols
{
    /// <summary>Material Symbols <c>move_selection_left</c>: a solid pane on the left, a dotted one on the right.</summary>
    public const string MoveSelectionLeft = "<path d=\"M2 18V6h12v12H2zm2-2h8V8H4v8zm12-8V6h2v2h-2zm0 10v-2h2v2h-2zm4-10V6h2v2h-2zm0 5v-2h2v2h-2zm0 5v-2h2v2h-2z\"/>";

    /// <summary>Material Symbols <c>move_selection_right</c>: a dotted pane on the left, a solid one on the right.</summary>
    public const string MoveSelectionRight = "<path d=\"M10 18V6h12v12H10zm2-2h8V8h-8v8zm-6 2v-2h2v2H6zm0-10V6h2v2H6zM2 18v-2h2v2H2zm0-5v-2h2v2H2zm0-5V6h2v2H2z\"/>";

    /// <summary>Material Symbols <c>newsstand</c>: three books standing on a shelf and one leaning, the Library's icon.</summary>
    public const string Newsstand = "<path d=\"M2 20v-2h20v2H2zm2-4V8h2v8H4zm4 0V4h2v12H8zm4 0V4h2v12h-2zm7 0L15 9l1.75-1 4 7-1.75 1z\"/>";

    /// <summary>Material Symbols <c>dock_to_left</c>: an outlined frame with a docked pane along its left edge.</summary>
    public const string DockToLeft = "<path d=\"M5 21q-.825 0-1.4125-.5875T3 19V5q0-.825.5875-1.4125T5 3h14q.825 0 1.4125.5875T21 5v14q0 .825-.5875 1.4125T19 21H5zm11-2h3V5h-3v14zm-2 0V5H5v14h9zm2 0h3-3z\"/>";
}
