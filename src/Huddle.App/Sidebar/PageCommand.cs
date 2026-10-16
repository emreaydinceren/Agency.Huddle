namespace Agency.Huddle.App.Sidebar;

/// <summary>A command a sidebar panel sends to the page of its Subject (Spec D-6).</summary>
public abstract record PageCommand;

/// <summary>Asks the document page to scroll a heading into view.</summary>
/// <param name="HeadingId">The heading's id in the outline.</param>
/// <param name="Line">The 1-based line of the heading in the document's whole text, frontmatter included.</param>
public sealed record ScrollToHeading(string HeadingId, int Line) : PageCommand;
