namespace Agency.Huddle.App.Themes;

/// <summary>
/// The application's own stylesheets, in the order <c>Components/App.razor</c> links them. The order is the
/// cascade order: a later file wins a tie in specificity, so it is part of what the files mean. Each path is
/// relative to <c>wwwroot</c> and is linked through <c>@Assets[...]</c> so the browser gets a fingerprinted
/// URL. A test pins that this list equals the <c>.css</c> files in <c>wwwroot/css</c>, so a file cannot be
/// added to the folder without being linked, or linked without existing.
/// </summary>
internal static class AppStylesheets
{
    /// <summary>The folder, relative to <c>wwwroot</c>, that holds the stylesheets.</summary>
    internal const string Folder = "css";

    /// <summary>Every stylesheet path relative to <c>wwwroot</c>, in link order.</summary>
    internal static IReadOnlyList<string> All { get; } =
    [
        "css/01-base.css",
        "css/02-shell.css",
        "css/03-room.css",
        "css/04-task-read.css",
        "css/05-room-traits.css",
        "css/06-agents.css",
        "css/07-settings.css",
        "css/08-tasks.css",
        "css/09-library.css",
        "css/10-room-cards.css",
        "css/11-tasks-more.css",
        "css/12-teams.css",
        "css/13-sidebar.css",
        "css/14-references.css",
        "css/15-task-form.css",
        "css/16-traits.css",
    ];
}
