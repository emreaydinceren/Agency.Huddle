namespace Agency.Huddle.App.Components.Tasks;

/// <summary>
/// Which of <c>TaskDetail</c>'s two layouts (Spec §13.6) is shown: the same fields and logic, only the
/// layout differs. Public (corrections-B5 D14 item 1): <c>TaskDetail</c> takes it as a <c>Mode</c>-typed <c>[Parameter]</c>.
/// </summary>
public enum TaskDetailMode
{
    /// <summary>The single-column layout inside the Tasks page's end drawer.</summary>
    Panel,

    /// <summary>The two-column layout inside <c>TaskDetailDialog</c>.</summary>
    Expanded,
}
