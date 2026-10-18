using Agency.Huddle.App.Components.Shared.Dialogs;
using Agency.Huddle.App.Skills.Ui;
using Agency.Huddle.App.SkillSources;
using Agency.Huddle.SkillPacks.Resolving;
using MudBlazor;

namespace Agency.Huddle.App.Components.Settings.Skills;

/// <summary>Remove a Pack from the card and from Details (Skill Packs spec §6.12.2): a confirm when nobody holds it,
/// <see cref="RemovePackDialog"/> when Teammates do, then the removal, a re-read and the outcome.</summary>
internal static class PackRemoval
{
    /// <summary>The snackbar key every removal shares.</summary>
    public const string SnackbarKey = "skill-removed";

    /// <summary>Asks, removes, re-reads and reports.</summary>
    public static async Task RemoveAsync(IDialogService dialogs, SkillSourcesRemover remover, SkillRegistry registry, ISnackbar snackbar, PackEntry pack, string title, IReadOnlyList<string> holders)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(remover);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(snackbar);
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(holders);

        HolderChoice choice = HolderChoice.None;
        if (holders.Count == 0)
        {
            string? file = pack.FilePath;
            bool inside = file is not null && SkillSourcesRemover.IsInside(file, registry.SkillSourcesDir);
            string message = RemovalText.PackMessage(inside && file is not null ? Path.GetRelativePath(registry.SkillSourcesDir, file) : null, inside ? null : file);
            if (!await dialogs.ConfirmAsync(RemovalText.PackTitle(title), message, "Remove", danger: true))
            {
                return;
            }
        }
        else
        {
            DialogParameters<RemovePackDialog> parameters = new() { { dialog => dialog.Holders, string.Join(", ", holders) } };
            IDialogReference reference = await dialogs.ShowAsync<RemovePackDialog>(RemovalText.PackTitle(title), parameters, HuddleDialogOptions.SmallForm);
            DialogResult? result = await reference.Result;
            if (result is null || result.Canceled || result.Data is not HolderChoice picked)
            {
                return;
            }

            choice = picked;
        }

        RemovalOutcome outcome = remover.RemovePack(pack, choice);
        await registry.RebuildAsync(RebuildScope.All, CancellationToken.None);
        snackbar.Add(outcome.Message, outcome.Ok ? Severity.Success : Severity.Error, configure: null, key: SnackbarKey);
    }
}

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

        await registry.RebuildAsync(RebuildScope.All, CancellationToken.None);
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
