using MudBlazor;

namespace Agency.Huddle.App.Components.Shared.Dialogs;

/// <summary>
/// The one "New project" prompt (UX-R14, OP-2): the shared <see cref="NameDialog"/> titled "New project", labelled
/// "Project name", with the verb "Create". The Teams nav and the Library tree both open it; each supplies only its
/// own validator and what creating does.
/// </summary>
internal static class NewProjectPrompt
{
    /// <summary>The dialog title.</summary>
    public const string Title = "New project";

    /// <summary>The field label.</summary>
    public const string Label = "Project name";

    /// <summary>The confirm button's verb.</summary>
    public const string ConfirmText = "Create";

    /// <summary>Shows the prompt and returns the trimmed name it closed with, or <see langword="null"/> when it was cancelled.</summary>
    /// <param name="dialogs">The dialog service.</param>
    /// <param name="validate">The settled validator over the trimmed name.</param>
    /// <param name="submit">Runs on Create; returns an error to keep the dialog open, or <see langword="null"/> to close it.</param>
    public static async Task<string?> AskAsync(IDialogService dialogs, Func<string, string?> validate, Func<string, Task<string?>> submit)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(validate);
        ArgumentNullException.ThrowIfNull(submit);

        DialogParameters<NameDialog> parameters = new()
        {
            { x => x.Label, Label },
            { x => x.ConfirmText, ConfirmText },
            { x => x.Validate, validate },
            { x => x.SubmitAsync, submit },
        };
        IDialogReference reference = await dialogs.ShowAsync<NameDialog>(Title, parameters, HuddleDialogOptions.SmallForm);
        DialogResult? result = await reference.Result;
        return result is { Canceled: false, Data: string name } ? name : null;
    }
}
