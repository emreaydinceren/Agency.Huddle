namespace Agency.Huddle.App.Tabs;

/// <summary>
/// Material Symbols that MudBlazor's classic Material Icons set does not ship, rescaled from Google's
/// 960-unit grid to the 24dp grid a <c>MudIcon</c> expects, so they render alongside <c>Icons.Material.Filled</c>.
/// </summary>
internal static class MaterialSymbols
{
    /// <summary>A fish facing left with a forked tail and an eye: goldfish mode (a fresh-eyes Agent), used for that concept only.</summary>
    public const string Fish = "<path d=\"M2 12c3.5-4.5 7.5-6.5 12-6.5 2.2 0 4.2 1.1 5.8 3.2L22 7v10l-2.2-1.7c-1.6 2.1-3.6 3.2-5.8 3.2-4.5 0-8.5-2-12-6.5zm5.5-1.5a1 1 0 1 0 0 2 1 1 0 0 0 0-2z\"/>";

    /// <summary>Material Symbols <c>move_selection_left</c>: a solid pane on the left, a dotted one on the right.</summary>
    public const string MoveSelectionLeft = "<path d=\"M2 18V6h12v12H2zm2-2h8V8H4v8zm12-8V6h2v2h-2zm0 10v-2h2v2h-2zm4-10V6h2v2h-2zm0 5v-2h2v2h-2zm0 5v-2h2v2h-2z\"/>";

    /// <summary>Material Symbols <c>move_selection_right</c>: a dotted pane on the left, a solid one on the right.</summary>
    public const string MoveSelectionRight = "<path d=\"M10 18V6h12v12H10zm2-2h8V8h-8v8zm-6 2v-2h2v2H6zm0-10V6h2v2H6zM2 18v-2h2v2H2zm0-5v-2h2v2H2zm0-5V6h2v2H2z\"/>";

    /// <summary>Material Symbols <c>dock_to_left</c>: an outlined frame with a docked pane along its left edge.</summary>
    public const string DockToLeft = "<path d=\"M5 21q-.825 0-1.4125-.5875T3 19V5q0-.825.5875-1.4125T5 3h14q.825 0 1.4125.5875T21 5v14q0 .825-.5875 1.4125T19 21H5zm11-2h3V5h-3v14zm-2 0V5H5v14h9zm2 0h3-3z\"/>";
}
