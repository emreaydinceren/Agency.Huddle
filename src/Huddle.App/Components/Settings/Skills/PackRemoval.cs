using Agency.Huddle.App.Components.Shared.Dialogs;
using Agency.Huddle.App.Skills.Ui;
using Agency.Huddle.App.SkillSources;
using Agency.Huddle.SkillPacks.Resolving;
using MudBlazor;

namespace Agency.Huddle.App.Components.Settings.Skills;

/// <summary>Remove a Pack from the card and from Details (Skill Packs spec §6.12.2): a confirm when nobody holds it,
/// <see cref="RemovePackDialog"/> when Agents do, then the removal, a re-read and the outcome.</summary>
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
            if (!await dialogs.ConfirmAsync(RemovalText.PackTitle(title), message, "Remove", danger: true).ConfigureAwait(false))
            {
                return;
            }
        }
        else
        {
            DialogParameters<RemovePackDialog> parameters = new() { { dialog => dialog.Holders, string.Join(", ", holders) } };
            IDialogReference reference = await dialogs.ShowAsync<RemovePackDialog>(RemovalText.PackTitle(title), parameters, HuddleDialogOptions.SmallForm).ConfigureAwait(false);
            DialogResult? result = await reference.Result.ConfigureAwait(false);
            if (result is null || result.Canceled || result.Data is not HolderChoice picked)
            {
                return;
            }

            choice = picked;
        }

        RemovalOutcome outcome = remover.RemovePack(pack, choice);
        await registry.RebuildAsync(RebuildScope.All, CancellationToken.None).ConfigureAwait(false);
        snackbar.Add(outcome.Message, outcome.Ok ? Severity.Success : Severity.Error, configure: null, key: SnackbarKey);
    }
}
