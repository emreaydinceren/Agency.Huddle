using System.Globalization;
using MudBlazor;

namespace Agency.Huddle.App.Components.Shared.Dialogs;

/// <summary>
/// The menu-opens-dialog idiom (design-system "Menus", audit B10e, B10f, E8). A <c>MudMenu</c> item that opens a
/// dialog closes its menu first, so the menu's popover is gone before the dialog's scrim appears; left open it
/// stays above the scrim. A call site keeps an <c>@ref</c> to its menu and wraps the dialog call in
/// <see cref="AfterClosingAsync{T}(MudMenu?, Func{Task{T}})"/>. The activator of such a menu carries no hover
/// tooltip, which would stay up over the menu's first item; it carries <see cref="ActivatorLabel(string)"/> as its
/// <c>aria-label</c> instead.
/// </summary>
internal static class MenuDialogs
{
    /// <summary>Closes <paramref name="menu"/> (when there is one), then runs <paramref name="openDialog"/> and returns its result.</summary>
    /// <typeparam name="T">What the dialog call returns, such as the confirm's <see cref="bool"/> or a dialog reference.</typeparam>
    /// <param name="menu">The menu whose item was pressed, from its <c>@ref</c>; <see langword="null"/> when the item is not in a menu, which just runs the call.</param>
    /// <param name="openDialog">The call that shows the dialog, for example <c>() =&gt; this.Dialogs.ConfirmAsync(...)</c>.</param>
    /// <returns>The result of <paramref name="openDialog"/>.</returns>
    public static async Task<T> AfterClosingAsync<T>(MudMenu? menu, Func<Task<T>> openDialog)
    {
        ArgumentNullException.ThrowIfNull(openDialog);

        if (menu is not null)
        {
            await menu.CloseMenuAsync();
        }

        return await openDialog();
    }

    /// <summary>The same as <see cref="AfterClosingAsync{T}(MudMenu?, Func{Task{T}})"/> for a call with no result.</summary>
    /// <param name="menu">The menu whose item was pressed, or <see langword="null"/>.</param>
    /// <param name="openDialog">The call that shows the dialog.</param>
    /// <returns>A task that completes when <paramref name="openDialog"/> does.</returns>
    public static async Task AfterClosingAsync(MudMenu? menu, Func<Task> openDialog)
    {
        ArgumentNullException.ThrowIfNull(openDialog);

        if (menu is not null)
        {
            await menu.CloseMenuAsync();
        }

        await openDialog();
    }

    /// <summary>The <c>aria-label</c> of a menu's activator, naming what the menu acts on: <c>Actions for Platform</c>.</summary>
    /// <param name="target">The name of the row, Team, View or folder the menu belongs to.</param>
    /// <returns>The accessible name.</returns>
    public static string ActivatorLabel(string target) => string.Create(CultureInfo.InvariantCulture, $"Actions for {target}");
}
