using MudBlazor;
using Agency.Huddle.App.Components.Shared.Dialogs;

namespace Agency.Huddle.Tests.Ui.Dialogs;

/// <summary>Pins the two shared dialog option sets (design-system "Dialog anatomy"): every dialog that adopts one keeps a close button and Escape-to-close.</summary>
public sealed class HuddleDialogOptionsTests
{
    /// <summary>SmallForm has a close button, closes on Escape, and ignores backdrop clicks so typed text is not lost.</summary>
    [Fact]
    public void SmallForm_HasCloseButtonAndEscape_ButNoBackdropDismissal()
    {
        DialogOptions options = HuddleDialogOptions.SmallForm;

        Assert.True(options.CloseButton);
        Assert.True(options.CloseOnEscapeKey);
        Assert.False(options.BackdropClick);
        Assert.Equal(MaxWidth.Small, options.MaxWidth);
    }

    /// <summary>Picker has a close button, closes on Escape, and a backdrop click may dismiss it.</summary>
    [Fact]
    public void Picker_HasCloseButtonAndEscape_AndBackdropDismisses()
    {
        DialogOptions options = HuddleDialogOptions.Picker;

        Assert.True(options.CloseButton);
        Assert.True(options.CloseOnEscapeKey);
        Assert.NotEqual(false, options.BackdropClick);
        Assert.Equal(MaxWidth.Medium, options.MaxWidth);
    }
}
