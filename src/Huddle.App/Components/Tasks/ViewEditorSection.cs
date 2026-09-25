namespace Agency.Huddle.App.Components.Tasks;

/// <summary>
/// Which section of <see cref="ViewEditorDrawer"/> should render as initially active when it opens
/// (corrections-B7 §13.2 item 1). A <see cref="ViewEditorDrawer.Section"/> parameter of an
/// <see langword="internal"/> type would fail <c>CS0053</c> (rules.md L59), so this is public even
/// though the drawer itself is the only public surface around it.
/// </summary>
public enum ViewEditorSection
{
    /// <summary>No section is singled out.</summary>
    General,

    /// <summary>The Columns section (Board only) - the Board's "Edit columns…" menu opens here (12.4.i).</summary>
    Columns,
}
