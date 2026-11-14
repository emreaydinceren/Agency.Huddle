using Microsoft.JSInterop;

namespace Agency.Huddle.App.Components.Shared;

/// <summary>
/// The one place that reads and writes the browser's <c>localStorage</c> through the
/// <c>get</c> and <c>set</c> exports of <see cref="JsModule.Storage"/>. Remembered view state (open panels, folders, tabs,
/// the last View) is a convenience, never a requirement, so every failure to reach storage reads as
/// "nothing remembered" rather than a fault. The services are arguments so the helper holds no state.
/// </summary>
/// <remarks>
/// Swallowed, and why: <see cref="JSDisconnectedException"/> (the circuit is gone, nothing is left to
/// draw or remember for), <see cref="JSException"/> (storage is blocked or throws: private window,
/// policy, quota) and <see cref="InvalidOperationException"/> (prerendering has no JavaScript).
/// Cancellation is not swallowed, because no caller cancels these calls; a caller that starts to must
/// decide what a cancelled read means. Anything else is a real fault and propagates. The helper does not
/// use <c>ConfigureAwait(false)</c> (it lives under <c>Components/</c>); a non-component caller that
/// wants it adds it to its own await.
/// </remarks>
internal static class BrowserStorage
{
    /// <summary>The storage module's read function.</summary>
    internal const string GetFunction = "get";

    /// <summary>The storage module's write function.</summary>
    internal const string SetFunction = "set";

    /// <summary>Reads <paramref name="key"/>.</summary>
    /// <param name="js">The JavaScript runtime that owns the storage helper.</param>
    /// <param name="key">The <c>localStorage</c> key.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The stored text, or <see langword="null"/> when nothing is stored or storage cannot be reached.</returns>
    internal static async Task<string?> TryGetAsync(IJSRuntime js, string key, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(js);
        ArgumentNullException.ThrowIfNull(key);

        try
        {
            IJSObjectReference storage = await JsModule.Storage.ImportAsync(js, ct);
            return await storage.InvokeAsync<string?>(BrowserStorage.GetFunction, ct, key);
        }
        catch (Exception ex) when (BrowserStorage.IsStorageUnavailable(ex))
        {
            return null;
        }
    }

    /// <summary>Writes <paramref name="value"/> under <paramref name="key"/>.</summary>
    /// <param name="js">The JavaScript runtime that owns the storage helper.</param>
    /// <param name="key">The <c>localStorage</c> key.</param>
    /// <param name="value">The text to remember.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns><see langword="true"/> when the write reached storage; <see langword="false"/> when storage could not be reached.</returns>
    internal static async Task<bool> TrySetAsync(IJSRuntime js, string key, string value, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(js);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        try
        {
            IJSObjectReference storage = await JsModule.Storage.ImportAsync(js, ct);
            await storage.InvokeVoidAsync(BrowserStorage.SetFunction, ct, key, value);
            return true;
        }
        catch (Exception ex) when (BrowserStorage.IsStorageUnavailable(ex))
        {
            return false;
        }
    }

    /// <summary>Whether <paramref name="ex"/> means storage cannot be reached rather than that the code is wrong.</summary>
    private static bool IsStorageUnavailable(Exception ex) => ex is JSDisconnectedException or JSException or InvalidOperationException;
}
