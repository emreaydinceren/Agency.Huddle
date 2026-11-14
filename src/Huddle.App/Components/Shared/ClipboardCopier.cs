using Microsoft.JSInterop;
using MudBlazor;

namespace Agency.Huddle.App.Components.Shared;

/// <summary>
/// The one place that copies text through the <c>copy</c> export of <see cref="JsModule.Clipboard"/> and, when
/// asked, tells the Human how it went. Every copy entry point (Room link, Task id and link, skill path,
/// gateway link) goes through it, so a dead circuit and a refused clipboard are handled the same way
/// everywhere. The services are arguments so the helper holds no state of its own.
/// </summary>
internal static class ClipboardCopier
{
    /// <summary>The clipboard module's copy function.</summary>
    internal const string CopyFunction = "copy";

    /// <summary>
    /// Puts <paramref name="text"/> on the clipboard. A circuit that is already gone
    /// (<see cref="JSDisconnectedException"/>) means there is nobody to tell, so it counts as not copied.
    /// </summary>
    /// <param name="js">The JavaScript runtime that owns the clipboard helper.</param>
    /// <param name="text">The text to copy.</param>
    /// <param name="ct">Cancels the clipboard call.</param>
    /// <returns><see langword="true"/> when the browser accepted the copy.</returns>
    internal static async Task<bool> TryCopyAsync(IJSRuntime js, string text, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(js);
        ArgumentNullException.ThrowIfNull(text);

        return await ClipboardCopier.InvokeAsync(js, text, ct) == true;
    }

    /// <summary>Calls the helper; <see langword="null"/> means the circuit was already gone.</summary>
    private static async Task<bool?> InvokeAsync(IJSRuntime js, string text, CancellationToken ct)
    {
        try
        {
            IJSObjectReference clipboard = await JsModule.Clipboard.ImportAsync(js, ct);
            return await clipboard.InvokeAsync<bool>(ClipboardCopier.CopyFunction, ct, text);
        }
        catch (JSDisconnectedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Copies <paramref name="text"/> and reports the outcome through the snackbar under
    /// <paramref name="key"/>, so a second copy replaces the first message. Shows nothing when the
    /// circuit is gone, since no snackbar is left to draw it.
    /// </summary>
    /// <param name="js">The JavaScript runtime that owns the clipboard helper.</param>
    /// <param name="snackbar">Where the outcome is reported.</param>
    /// <param name="text">The text to copy.</param>
    /// <param name="copiedMessage">What the success snackbar says.</param>
    /// <param name="failedMessage">What the warning snackbar says when the clipboard refuses.</param>
    /// <param name="key">The snackbar key; a message with the same key replaces the earlier one.</param>
    /// <param name="ct">Cancels the clipboard call.</param>
    /// <returns><see langword="true"/> when the browser accepted the copy.</returns>
    internal static async Task<bool> CopyAsync(
        IJSRuntime js,
        ISnackbar snackbar,
        string text,
        string copiedMessage,
        string failedMessage,
        string key,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(js);
        ArgumentNullException.ThrowIfNull(snackbar);
        ArgumentNullException.ThrowIfNull(text);

        bool? outcome = await ClipboardCopier.InvokeAsync(js, text, ct);
        if (outcome is not { } copied)
        {
            return false;
        }

        if (copied)
        {
            snackbar.Add(copiedMessage, Severity.Success, configure: null, key: key);
        }
        else
        {
            snackbar.Add(failedMessage, Severity.Warning, configure: null, key: key);
        }

        return copied;
    }
}
