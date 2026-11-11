using Agency.Huddle.App.Components.Shared;
using Microsoft.JSInterop;
using MudBlazor;

namespace Agency.Huddle.App.Skills.Ui;

/// <summary>Copies a prompt for the Human's AI tool and says what happens next (Skill Packs spec §6.9.2): the same
/// <c>huddleClipboard.copy</c> helper <c>TaskClipboard</c> uses. The services are arguments, so it holds no state.</summary>
internal static class PromptClipboard
{
    /// <summary>The snackbar key every prompt copy shares, so a second copy replaces the first message.</summary>
    public const string SnackbarKey = "skill-prompt-copied";

    /// <summary>The message after a copy.</summary>
    public const string CopiedMessage = "Prompt copied. Paste it into your AI tool.";

    /// <summary>The message when the browser refuses the copy.</summary>
    public const string RefusedMessage = "Couldn't copy the prompt. Select it and press Ctrl+C.";

    private const string CopyHelper = "huddleClipboard.copy";

    /// <summary>Copies <paramref name="text"/> and shows the outcome.</summary>
    /// <returns>Whether the browser took the text.</returns>
    public static async Task<bool> CopyAsync(IJSRuntime js, ISnackbar snackbar, string text, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(js);
        ArgumentNullException.ThrowIfNull(snackbar);
        ArgumentNullException.ThrowIfNull(text);

        (_, bool copied) = await BrowserCall.InvokeAsync<bool>(js, CopyHelper, ct, text).ConfigureAwait(false);
        if (copied)
        {
            snackbar.Add(CopiedMessage, Severity.Success, configure: null, key: SnackbarKey);
        }
        else
        {
            snackbar.Add(RefusedMessage, Severity.Warning, configure: null, key: SnackbarKey);
        }

        return copied;
    }
}
