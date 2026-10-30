using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Gateway;
using Agency.Huddle.Tests.Gateway;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Remote access spec 6.15 (OP-7): the folder picker lists the host's folders, so it lists nothing and says why while remote access is configured.</summary>
public sealed class FolderPickerRemoteAccessTests
{
    private static async Task<IRenderedComponent<ContainerFragment>> OpenPickerAsync(MudBunitContext ctx, bool configured, string start)
    {
        FakeRemoteAccessState state = new(configured);
        ctx.Services.AddSingleton<IRemoteAccessState>(state);
        ctx.Services.AddSingleton(new HostActions(state));
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        IDialogService dialogs = ctx.Services.GetRequiredService<IDialogService>();
        DialogParameters<FolderPickerDialog> parameters = new() { { x => x.Start, start } };

        _ = await cut.InvokeAsync(() => dialogs.ShowAsync<FolderPickerDialog>("Choose a folder", parameters, FolderPickerDialog.Options));
        return cut;
    }

    /// <summary>Configured: no folder rows, the refusal is shown and nothing can be chosen.</summary>
    [Fact]
    public async Task FolderPicker_RemoteAccessConfigured_ShowsTheRefusalAndNoFolders()
    {
        await using MudBunitContext ctx = new();
        using TempDataDir dir = new();
        Directory.CreateDirectory(Path.Combine(dir.Path, "secret-project"));

        IRenderedComponent<ContainerFragment> cut = await OpenPickerAsync(ctx, configured: true, dir.Path);

        cut.WaitForAssertion(() => Assert.Equal(HostActions.RefusedMessage, cut.Find(".folder-picker-error").TextContent.Trim()));
        Assert.Empty(cut.FindAll(".picker-row"));
        Assert.DoesNotContain("secret-project", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>Off: the picker lists as before.</summary>
    [Fact]
    public async Task FolderPicker_RemoteAccessOff_ListsFolders()
    {
        await using MudBunitContext ctx = new();
        using TempDataDir dir = new();
        Directory.CreateDirectory(Path.Combine(dir.Path, "alpha"));

        IRenderedComponent<ContainerFragment> cut = await OpenPickerAsync(ctx, configured: false, dir.Path);

        cut.WaitForAssertion(() => Assert.Equal("alpha", Assert.Single(cut.FindAll(".picker-row")).TextContent.Trim()));
        Assert.DoesNotContain(HostActions.RefusedMessage, cut.Markup, StringComparison.Ordinal);
    }
}
