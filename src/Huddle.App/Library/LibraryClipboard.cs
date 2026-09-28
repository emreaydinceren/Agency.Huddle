using Microsoft.JSInterop;
using MudBlazor;

namespace Agency.Huddle.App.Library;

/// <summary>
/// Copies a Library path to the clipboard through the shared <c>huddleClipboard.copy</c> JS helper
/// and shows the settled snackbar text (Spec §6.14; corrections-B6 item 28). Reused by
/// <c>LibraryDocument</c>'s Copy button and <c>LibraryFileOps</c>'s <c>CopyPath</c> tree action so
/// both surfaces call the same helper and show identical wording.
/// </summary>
internal static class LibraryClipboard
{
    /// <summary>
    /// Copies <paramref name="fullPath"/> via <c>huddleClipboard.copy</c> and shows
    /// <c>"Path copied"</c> on success or <c>"Couldn't copy the path."</c> on failure, both keyed
    /// <c>library-copy</c> so a second copy replaces the first toast.
    /// </summary>
    /// <param name="js">The JS runtime to invoke <c>huddleClipboard.copy</c> through.</param>
    /// <param name="snackbar">The snackbar to show the result on.</param>
    /// <param name="fullPath">The absolute path to copy.</param>
    public static async Task CopyPathAsync(IJSRuntime js, ISnackbar snackbar, string fullPath)
    {
        ArgumentNullException.ThrowIfNull(js);
        ArgumentNullException.ThrowIfNull(snackbar);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        bool copied = await js.InvokeAsync<bool>("huddleClipboard.copy", fullPath);
        snackbar.Add(
            copied ? "Path copied" : "Couldn't copy the path.",
            copied ? Severity.Success : Severity.Warning,
            configure: null,
            key: "library-copy");
    }
}
