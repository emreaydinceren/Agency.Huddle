using MudBlazor;

namespace Agency.Huddle.App.Components.Shared.Dialogs;

/// <summary>The <see cref="DialogOptions"/> every dialog uses (design-system "Dialog anatomy"): 600px for forms, confirms and short pickers, 960px only for a two-pane or tabular picker.</summary>
internal static class HuddleDialogOptions
{
    /// <summary>A form or confirm: close button, small, full width, and the backdrop does not dismiss it so typed input is not lost.</summary>
    public static DialogOptions SmallForm { get; } = new()
    {
        CloseButton = true,
        MaxWidth = MaxWidth.Small,
        FullWidth = true,
        CloseOnEscapeKey = true,
        BackdropClick = false,
    };

    /// <summary>A short picker (under about 12 items, such as the folder picker): the same 600px as a form, close button, and the backdrop may dismiss it because nothing typed is at risk.</summary>
    public static DialogOptions ShortPicker { get; } = new()
    {
        CloseButton = true,
        MaxWidth = MaxWidth.Small,
        FullWidth = true,
        CloseOnEscapeKey = true,
    };

    /// <summary>A two-pane or tabular picker only (960px): close button, full width, and the backdrop may dismiss it. A list of one column belongs in <see cref="ShortPicker"/>.</summary>
    public static DialogOptions Picker { get; } = new()
    {
        CloseButton = true,
        MaxWidth = MaxWidth.Medium,
        FullWidth = true,
        CloseOnEscapeKey = true,
    };
}
