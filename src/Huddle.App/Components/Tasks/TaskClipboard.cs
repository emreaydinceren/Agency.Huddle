using Agency.Huddle.App.Tasks;
using Microsoft.JSInterop;
using MudBlazor;

namespace Agency.Huddle.App.Components.Tasks;

/// <summary>
/// Copies a Task's id or link to the clipboard and reports the outcome, for every surface that offers
/// Copy id or Copy link (UX-R04). The services are arguments so the helper holds no state of its own.
/// </summary>
internal static class TaskClipboard
{
    /// <summary>The clipboard helper the copy calls.</summary>
    private const string CopyHelper = "huddleClipboard.copy";

    /// <summary>Copies <paramref name="id"/> as text and says "Copied {id}", or the Ctrl+C warning when the clipboard refuses.</summary>
    /// <param name="js">The JavaScript runtime that owns the clipboard helper.</param>
    /// <param name="snackbar">Where the outcome is reported.</param>
    /// <param name="id">The Task id to copy.</param>
    /// <param name="ct">Cancels the clipboard call.</param>
    /// <returns>A task that completes once the outcome is shown.</returns>
    internal static async Task CopyIdAsync(IJSRuntime js, ISnackbar snackbar, TaskId id, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(js);
        ArgumentNullException.ThrowIfNull(snackbar);

        bool copied = await js.InvokeAsync<bool>(TaskClipboard.CopyHelper, ct, new object?[] { id.ToString() });
        if (copied)
        {
            snackbar.Add($"Copied {id}", Severity.Success, configure: null, key: "copy");
        }
        else
        {
            snackbar.Add("Couldn't copy. The id is selected; press Ctrl+C.", Severity.Warning, configure: null, key: "copy");
        }
    }

    /// <summary>Copies the link that opens <paramref name="id"/> and says "Copied link to {id}", or that the link could not be copied.</summary>
    /// <param name="js">The JavaScript runtime that owns the clipboard helper.</param>
    /// <param name="snackbar">Where the outcome is reported.</param>
    /// <param name="baseUri">The app's base address, ending in a slash.</param>
    /// <param name="id">The Task id the link opens.</param>
    /// <param name="ct">Cancels the clipboard call.</param>
    /// <returns>A task that completes once the outcome is shown.</returns>
    internal static async Task CopyLinkAsync(IJSRuntime js, ISnackbar snackbar, string baseUri, TaskId id, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(js);
        ArgumentNullException.ThrowIfNull(snackbar);
        ArgumentNullException.ThrowIfNull(baseUri);

        bool copied = await js.InvokeAsync<bool>(TaskClipboard.CopyHelper, ct, new object?[] { $"{baseUri}tasks/item/{id}" });
        if (copied)
        {
            snackbar.Add($"Copied link to {id}", Severity.Success, configure: null, key: "copy");
        }
        else
        {
            snackbar.Add("Couldn't copy the link.", Severity.Warning, configure: null, key: "copy");
        }
    }
}
