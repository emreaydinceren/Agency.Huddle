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
public sealed record NewTaskDefaults(string? Team, string? Project);
