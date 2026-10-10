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
}
