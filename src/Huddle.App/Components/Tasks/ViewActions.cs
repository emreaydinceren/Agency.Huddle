using Agency.Huddle.App.Components.Shared.Dialogs;
using Agency.Huddle.App.Tasks.Views;
using MudBlazor;

namespace Agency.Huddle.App.Components.Tasks;

/// <summary>The View actions the three delete entry points share: the Tasks page's menu, the View editor and the sidebar row menu (UX-R06).</summary>
internal static class ViewActions
{
    /// <summary>Asks whether to delete <paramref name="view"/> through the shared confirm and returns the answer.</summary>
    /// <param name="dialogService">The dialog service.</param>
    /// <param name="view">The View about to be deleted.</param>
    /// <param name="ct">Cancels the wait.</param>
    /// <returns>Whether the user confirmed.</returns>
    public static Task<bool> ConfirmDeleteAsync(IDialogService dialogService, TaskView view, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(view);

        return dialogService.ConfirmAsync(
            $"Delete '{view.Name}'?",
            "This removes the View. Tasks are not affected.",
            "Delete",
            danger: true,
            ct);
    }
}
