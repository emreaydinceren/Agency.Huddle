using MudBlazor;

namespace Agency.Huddle.App.Components.Shared.Dialogs;

/// <summary>The two <see cref="DialogOptions"/> every dialog uses (design-system "Dialog anatomy").</summary>
internal static class HuddleDialogOptions
{
    /// <summary>A form or confirm: close button, small, full width, and the backdrop does not dismiss it so typed input is not lost.</summary>
    public static DialogOptions SmallForm { get; } = new()
    {
        CloseButton = true,
        MaxWidth = MaxWidth.Small,
        FullWidth = true,
        BackdropClick = false,
    };

    /// <summary>A picker or list: close button, medium, full width, and the backdrop may dismiss it because nothing typed is at risk.</summary>
    public static DialogOptions Picker { get; } = new()
    {
        CloseButton = true,
        MaxWidth = MaxWidth.Medium,
        FullWidth = true,
    };
}
