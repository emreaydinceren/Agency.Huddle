using Agency.Huddle.App.SkillSources;
using MudBlazor;

namespace Agency.Huddle.App.Components.Settings.Skills;

/// <summary>Hide and Unhide (Skill Packs spec §6.12.4): no confirm, since nothing is deleted; the snackbar offers Undo.</summary>
internal static class HideFlow
{
    /// <summary>The snackbar key.</summary>
    public const string SnackbarKey = "skill-hidden";

    /// <summary>The Error snackbar when <c>hidden.json</c> cannot be read.</summary>
    public const string RefusedMessage = "hidden.json could not be read, so nothing was changed. Fix or delete it.";

    /// <summary>Hides or unhides, re-reads <c>hidden.json</c> and reports; a hide offers Undo.</summary>
    public static async Task SetAsync(SkillSourcesRemover remover, SkillRegistry registry, ISnackbar snackbar, HiddenKind kind, string name, string display, string? sourceId, bool hide)
    {
        ArgumentNullException.ThrowIfNull(remover);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(snackbar);
        HiddenWriteResult result = remover.SetHidden(kind, name, sourceId, hide);
        if (result == HiddenWriteResult.Refused)
        {
            snackbar.Add(RefusedMessage, Severity.Error, configure: null, key: SnackbarKey);
            return;
        }

        await registry.RebuildAsync(RebuildScope.All, CancellationToken.None).ConfigureAwait(false);
        if (!hide)
        {
            snackbar.Add($"{display} unhidden", Severity.Success, configure: null, key: SnackbarKey);
            return;
        }

        snackbar.Add($"{display} hidden", Severity.Success, configure: options =>
        {
            options.Action = "Undo";
            options.OnClick = _ => SetAsync(remover, registry, snackbar, kind, name, display, sourceId, hide: false);
        }, key: SnackbarKey);
    }
}
