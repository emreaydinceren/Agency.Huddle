using Agency.Huddle.App.Data;
using Agency.Huddle.App.References;
using Microsoft.JSInterop;
using MudBlazor;

namespace Agency.Huddle.App.Components.Shared;

/// <summary>
/// Copies a Room's reference text to the clipboard and reports the outcome, for every entry point that
/// offers Copy link (UX-R03). The services are arguments so the helper holds no state of its own.
/// </summary>
internal static class RoomLinkCopier
{
    /// <summary>The clipboard helper the copy calls.</summary>
    private const string CopyHelper = "huddleClipboard.copy";

    /// <summary>
    /// Puts the Room's reference on the clipboard and shows the spec's wording through the snackbar,
    /// under the <c>copy</c> key so a second copy replaces the first message.
    /// </summary>
    /// <param name="js">The JavaScript runtime that owns the clipboard helper.</param>
    /// <param name="snackbar">Where the outcome is reported.</param>
    /// <param name="room">The Room whose link is copied.</param>
    /// <param name="ct">Cancels the clipboard call.</param>
    /// <returns>A task that completes once the outcome is shown.</returns>
    internal static async Task CopyAsync(IJSRuntime js, ISnackbar snackbar, Room room, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(js);
        ArgumentNullException.ThrowIfNull(snackbar);
        ArgumentNullException.ThrowIfNull(room);

        bool copied = await js.InvokeAsync<bool>(RoomLinkCopier.CopyHelper, ct, new object?[] { RoomLinkText.For(room) });
        if (copied)
        {
            snackbar.Add($"Copied link to {room.Name}", Severity.Success, configure: null, key: "copy");
        }
        else
        {
            snackbar.Add("Couldn't copy the link.", Severity.Warning, configure: null, key: "copy");
        }
    }
}
