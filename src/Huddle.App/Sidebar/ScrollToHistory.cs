namespace Agency.Huddle.App.Sidebar;

/// <summary>Asks the document page to scroll its History into view (DocHistory spec §6.8). Ignored outside Read mode.</summary>
public sealed record ScrollToHistory : PageCommand;
