namespace Agency.Huddle.App.Components.Tasks;

/// <summary>
/// The Team and Project a freshly opened <c>TaskDetail</c> create-mode dialog should default to
/// (Spec §13.3: "+ New task" pre-fills the Team when the toolbar's filter names exactly one). Public
/// because it is an <see cref="Microsoft.AspNetCore.Components.EventCallback{TValue}"/> payload
/// crossing a component boundary - see <c>TaskDraft</c>'s own reason in D6 for why a payload type
/// cannot stay <see langword="internal"/> (<c>CS0053</c>).
/// </summary>
/// <param name="Team">The Team to default to, or <see langword="null"/> when the filter names none or more than one.</param>
/// <param name="Project">The Project to default to, or <see langword="null"/> when the filter names none or more than one.</param>
public sealed record NewTaskDefaults(string? Team, string? Project)
{
    /// <summary>The defaults a "New task" in <paramref name="view"/> starts with: its filter's Team when it names exactly one, and its Project likewise.</summary>
    /// <param name="view">The effective View.</param>
    internal static NewTaskDefaults For(Agency.Huddle.App.Tasks.Views.TaskView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        string? team = view.Filter.Teams.Count == 1 ? view.Filter.Teams[0] : null;
        string? project = view.Filter.Projects.Count == 1 ? view.Filter.Projects[0].Project : null;
        return new NewTaskDefaults(team, project);
    }
}