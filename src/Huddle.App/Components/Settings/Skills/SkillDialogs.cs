using Agency.Huddle.App.Components.Shared.Dialogs;
using MudBlazor;

namespace Agency.Huddle.App.Components.Settings.Skills;

/// <summary>Opens the Skills dialogs the same way from every entry point (Skill Packs spec §6.9.7).</summary>
internal static class SkillDialogs
{
    /// <summary>The prompt dialog's title.</summary>
    public const string AddWithAiTitle = "Add with your AI tool";

    /// <summary>Opens <see cref="AddWithAiDialog"/> on a task, with what that entry point pre-fills.</summary>
    /// <param name="dialogs">The dialog service.</param>
    /// <param name="task"><c>add</c>, <c>find</c>, <c>update</c> or <c>fix</c>.</param>
    /// <param name="roleId">The role for <c>find</c>.</param>
    /// <param name="sourceIds">Comma-separated Source ids for <c>update</c>.</param>
    /// <param name="rowPath">The pack or catalog file for <c>fix</c>.</param>
    public static Task<IDialogReference> OpenAddWithAiAsync(IDialogService dialogs, string task, string? roleId = null, string? sourceIds = null, string? rowPath = null)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        DialogParameters<AddWithAiDialog> parameters = new()
        {
            { dialog => dialog.Task, task },
            { dialog => dialog.RoleId, roleId },
            { dialog => dialog.SourceIds, sourceIds },
            { dialog => dialog.RowPath, rowPath },
        };
        return dialogs.ShowAsync<AddWithAiDialog>(AddWithAiTitle, parameters, HuddleDialogOptions.SmallForm);
    }
}
