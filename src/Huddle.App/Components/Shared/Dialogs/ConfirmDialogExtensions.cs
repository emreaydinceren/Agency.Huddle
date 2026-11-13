using MudBlazor;

namespace Agency.Huddle.App.Components.Shared.Dialogs;

/// <summary>Opens <see cref="ConfirmDialog"/> as a yes/no question (design-system "Confirming", UX-R12).</summary>
internal static class ConfirmDialogExtensions
{
    /// <summary>Asks the user to confirm; returns <see langword="true"/> only when the confirm button was pressed.</summary>
    /// <param name="dialogService">The dialog service.</param>
    /// <param name="title">The title, a question ending in "?".</param>
    /// <param name="message">One sentence saying what is lost.</param>
    /// <param name="confirmText">The verb on the confirm button.</param>
    /// <param name="danger">Whether the confirm destroys something.</param>
    /// <param name="ct">Cancels the wait.</param>
    /// <returns>Whether the user confirmed.</returns>
    public static async Task<bool> ConfirmAsync(this IDialogService dialogService, string title, string message, string confirmText, bool danger = false, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dialogService);

        DialogParameters<ConfirmDialog> parameters = new()
        {
            { x => x.Message, message },
            { x => x.ConfirmText, confirmText },
            { x => x.Danger, danger },
        };

        IDialogReference reference = await dialogService.ShowAsync<ConfirmDialog>(title, parameters, HuddleDialogOptions.SmallForm);
        DialogResult? result = await reference.Result.WaitAsync(ct);
        return result is { Canceled: false };
    }

    /// <summary>Asks the user to confirm, listing <paramref name="points"/> under the message; returns <see langword="true"/> only when the confirm button was pressed.</summary>
    /// <param name="dialogService">The dialog service.</param>
    /// <param name="title">The title, a question ending in "?".</param>
    /// <param name="message">The sentence above the points.</param>
    /// <param name="points">The points, each with its lead in bold.</param>
    /// <param name="confirmText">The verb on the confirm button.</param>
    /// <param name="danger">Whether the confirm destroys something.</param>
    /// <param name="ct">Cancels the wait.</param>
    /// <returns>Whether the user confirmed.</returns>
    public static async Task<bool> ConfirmAsync(this IDialogService dialogService, string title, string message, IReadOnlyList<ConfirmPoint> points, string confirmText, bool danger = false, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dialogService);

        DialogParameters<ConfirmDialog> parameters = new()
        {
            { x => x.Message, message },
            { x => x.ConfirmText, confirmText },
            { x => x.Danger, danger },
            { x => x.Points, points },
        };

        IDialogReference reference = await dialogService.ShowAsync<ConfirmDialog>(title, parameters, HuddleDialogOptions.SmallForm);
        DialogResult? result = await reference.Result.WaitAsync(ct);
        return result is { Canceled: false };
    }
}
