using Agency.Huddle.App.Components.Shared.Dialogs;
using Agency.Huddle.App.Tasks.Views;
using MudBlazor;

namespace Agency.Huddle.App.Components.Tasks;

/// <summary>The View actions the entry points share: delete and rename from the Tasks page's menu and the sidebar row menu (UX-R06), and the default name of a new View.</summary>
internal static class ViewActions
{
    /// <summary>The longest a View's name may be, the limit <c>ViewValidator</c> holds every name to.</summary>
    private const int MaxNameLength = 60;

    /// <summary>
    /// Opens the shared <see cref="NameDialog"/> as "Rename view" for the saved View <paramref name="viewId"/>, prefilled with
    /// its name. The trimmed name is validated live against the same rules as <c>ViewValidator</c> (1-60 characters, unique
    /// ignoring case among the other Views), and Rename saves it through <paramref name="store"/>, which every View list
    /// follows. Does nothing when the View no longer exists.
    /// </summary>
    /// <param name="dialogService">The dialog service.</param>
    /// <param name="store">The View store the rename is saved to.</param>
    /// <param name="viewId">The id of the View to rename.</param>
    public static async Task RenameAsync(IDialogService dialogService, ViewStore store, string viewId)
    {
        ArgumentNullException.ThrowIfNull(dialogService);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(viewId);

        if (store.Get(viewId) is not { } view)
        {
            return;
        }

        DialogParameters<NameDialog> parameters = new()
        {
            { x => x.Label, "Name" },
            { x => x.ConfirmText, "Rename" },
            { x => x.InitialValue, view.Name },
            { x => x.Validate, name => NameProblem(store, viewId, name) },
            { x => x.SubmitAsync, name => Task.FromResult(Rename(store, viewId, name)) },
        };
        _ = await dialogService.ShowAsync<NameDialog>("Rename view", parameters, HuddleDialogOptions.SmallForm);
    }

    /// <summary>The problem with <paramref name="name"/> as the name of View <paramref name="viewId"/>, or <see langword="null"/> when it is fine.</summary>
    /// <param name="store">The View store, for the other Views' names.</param>
    /// <param name="viewId">The View being renamed, whose own current name does not count as a duplicate.</param>
    /// <param name="name">The trimmed name.</param>
    private static string? NameProblem(ViewStore store, string viewId, string name)
    {
        if (name.Length > MaxNameLength)
        {
            return $"Name must be 1-{MaxNameLength} characters.";
        }

        bool taken = store.Views.Any(other =>
            !string.Equals(other.Id, viewId, StringComparison.Ordinal) &&
            string.Equals(other.Name, name, StringComparison.OrdinalIgnoreCase));
        return taken ? "Another View already has this name." : null;
    }

    /// <summary>Saves <paramref name="name"/> as View <paramref name="viewId"/>'s name, returning the store's reasons when it refuses and <see langword="null"/> on success.</summary>
    /// <param name="store">The View store.</param>
    /// <param name="viewId">The View to rename.</param>
    /// <param name="name">The trimmed new name.</param>
    private static string? Rename(ViewStore store, string viewId, string name)
    {
        if (store.Get(viewId) is not { } view)
        {
            return "That View no longer exists.";
        }

        ViewSaveResult result = store.Save(view with { Name = name });
        return result.Saved ? null : string.Join(' ', result.Problems);
    }

    /// <summary>The name a created View starts with: "New view", or "New view 2", "New view 3" and so on - the first no other View holds, ignoring case. The Human renames it from the ⋮ menu.</summary>
    /// <param name="store">The View store, for the names already taken.</param>
    public static string NewViewName(ViewStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        HashSet<string> taken = new(store.Views.Select(static view => view.Name), StringComparer.OrdinalIgnoreCase);
        string candidate = "New view";
        for (int number = 2; taken.Contains(candidate); number++)
        {
            candidate = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"New view {number}");
        }

        return candidate;
    }

    /// <summary>Saves a new List View under <see cref="NewViewName"/>, as the Tasks page's New view route and the sidebar's Tasks "+" both do.</summary>
    /// <param name="store">The View store the View is saved to.</param>
    /// <returns>The saved View, or <see langword="null"/> when the store refused it (its <c>views.json</c> is unreadable).</returns>
    public static TaskView? CreateNew(ViewStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        TaskView created = new() { Id = Guid.NewGuid().ToString("N"), Name = NewViewName(store), Kind = ViewKind.List, Fields = ViewStore.DefaultListFields };
        return store.Save(created).Saved ? created : null;
    }

    /// <summary>Asks whether to delete <paramref name="view"/> through the shared confirm and returns the answer.</summary>
    /// <param name="dialogService">The dialog service.</param>
    /// <param name="view">The View about to be deleted.</param>
    /// <param name="ct">Cancels the wait.</param>
    /// <returns>Whether the user confirmed.</returns>
    public static Task<bool> ConfirmDeleteAsync(IDialogService dialogService, TaskView view, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(view);

        return dialogService.ConfirmAsync(
            ConfirmCopy.DeleteTitle(view.Name),
            "This removes the View. Tasks are not affected.",
            "Delete",
            danger: true,
            ct);
    }
}
