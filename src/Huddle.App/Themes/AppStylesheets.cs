namespace Agency.Huddle.App.Themes;

/// <summary>
/// The application's own stylesheets, in the order <c>Components/App.razor</c> links them. The order is the
/// cascade order: a later file wins a tie in specificity, so it is part of what the files mean. Each path is
/// relative to <c>wwwroot</c> and is linked through <c>@Assets[...]</c> so the browser gets a fingerprinted
/// URL. A test pins that this list holds exactly the <c>.css</c> files in <c>wwwroot/css</c>, so a file cannot be
/// added to the folder without being linked, or linked without existing. The files are named for the feature they
/// style, so their alphabetical order is not the link order: this list is the only place the order is written.
/// <c>base</c> comes first because every other file may override it. Reordering the rest can change which rule wins
/// a tie, so check a change to this order with <c>agents/scripts/Css-Snapshot.ps1</c>.
/// </summary>
internal static class AppStylesheets
{
    /// <summary>The folder, relative to <c>wwwroot</c>, that holds the stylesheets.</summary>
    internal const string Folder = "css";

    /// <summary>Every stylesheet path relative to <c>wwwroot</c>, in link order.</summary>
    internal static IReadOnlyList<string> All { get; } =
    [
        "css/base.css",
        "css/shell.css",
        "css/room.css",
        "css/agents.css",
        "css/settings.css",
        "css/library.css",
        "css/teams.css",
        "css/sidebar.css",
        "css/references.css",
        "css/tasks.css",
        "css/task-form.css",
    ];
}
