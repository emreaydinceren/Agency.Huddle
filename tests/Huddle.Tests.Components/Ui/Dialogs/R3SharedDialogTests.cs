using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Components.Shared.Dialogs;

namespace Agency.Huddle.Tests.Ui.Dialogs;

/// <summary>
/// Pins the round-3 dialog helpers (audit B10e-l): the 600px short-picker preset, the one quote style, the title
/// that names a location, the menu-opens-dialog idiom, and the folder picker's path trail and 32px picker rows.
/// </summary>
public sealed class R3SharedDialogTests
{
    /// <summary>ShortPicker is the 600px width a form has, with a close button, Escape and a dismissable backdrop; Picker stays the wider two-pane preset.</summary>
    [Fact]
    public void ShortPicker_IsAsNarrowAsAForm_AndPickerIsTheWiderOne()
    {
        DialogOptions options = HuddleDialogOptions.ShortPicker;

        Assert.Equal(MaxWidth.Small, options.MaxWidth);
        Assert.Equal(HuddleDialogOptions.SmallForm.MaxWidth, options.MaxWidth);
        Assert.True(options.CloseButton);
        Assert.True(options.CloseOnEscapeKey);
        Assert.True(options.FullWidth);
        Assert.NotEqual(false, options.BackdropClick);
        Assert.Equal(MaxWidth.Medium, HuddleDialogOptions.Picker.MaxWidth);
    }

    /// <summary>The folder picker is a short picker, so it opens at 600px.</summary>
    [Fact]
    public void FolderPicker_OpensAsAShortPicker()
    {
        Assert.Same(HuddleDialogOptions.ShortPicker, FolderPickerDialog.Options);
    }

    /// <summary>Copy quotes a name with double quotes only, and a delete title is a question ending in "?".</summary>
    [Fact]
    public void ConfirmCopy_QuotesWithDoubleQuotes()
    {
        Assert.Equal("\"Notes\"", ConfirmCopy.Quote("Notes"));
        Assert.Equal("Delete \"Notes\"?", ConfirmCopy.DeleteTitle("Notes"));
        Assert.DoesNotContain("'", ConfirmCopy.DeleteTitle("Notes"), StringComparison.Ordinal);
    }

    /// <summary>A NameDialog given a Location names it in the title: "New note" becomes "New note in Platform".</summary>
    [Fact]
    public async Task NameDialog_WithLocation_NamesThePlaceInTheTitle()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });

        await OpenNameDialogAsync(ctx, cut, "New note", "Platform");

        cut.WaitForAssertion(() => Assert.Equal("New note in Platform", cut.Find(".mud-dialog-title").TextContent.Trim()));
    }

    /// <summary>Without a Location the title is the caller's, unchanged.</summary>
    [Fact]
    public async Task NameDialog_WithoutLocation_KeepsTheCallersTitle()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });

        await OpenNameDialogAsync(ctx, cut, "New note", null);

        Assert.Equal("New note", cut.Find(".mud-dialog-title").TextContent.Trim());
    }

    /// <summary>With no menu (an item that is not in one) the idiom just runs the dialog call and returns its result.</summary>
    [Fact]
    public async Task AfterClosing_WithoutAMenu_RunsTheCall()
    {
        bool ran = false;

        bool result = await MenuDialogs.AfterClosingAsync<bool>(null, () =>
        {
            ran = true;
            return Task.FromResult(true);
        });

        Assert.True(ran);
        Assert.True(result);
    }

    /// <summary>The idiom closes the open menu before the dialog call runs, so no menu popover is open when the scrim appears.</summary>
    [Fact]
    public async Task AfterClosing_ClosesTheOpenMenuBeforeTheCall()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static builder =>
        {
            builder.OpenComponent<MudMenu>(0);
            builder.AddAttribute(1, nameof(MudMenu.Label), "Actions");
            builder.AddAttribute(2, nameof(MudMenu.ChildContent), (Microsoft.AspNetCore.Components.RenderFragment)(static item =>
            {
                item.OpenComponent<MudMenuItem>(0);
                item.AddAttribute(1, nameof(MudMenuItem.ChildContent), (Microsoft.AspNetCore.Components.RenderFragment)(static text => text.AddContent(0, "Delete")));
                item.CloseComponent();
            }));
            builder.CloseComponent();
        });
        MudMenu menu = cut.FindComponent<MudMenu>().Instance;
        await cut.InvokeAsync(() => cut.Find("button").ClickAsync());
        Assert.NotEmpty(cut.FindAll("div.mud-menu-item"));
        int openItemsWhenTheDialogOpens = -1;

        await cut.InvokeAsync(() => MenuDialogs.AfterClosingAsync<bool>(menu, () =>
        {
            openItemsWhenTheDialogOpens = cut.FindAll("div.mud-menu-item").Count;
            return Task.FromResult(true);
        }));

        Assert.Equal(0, openItemsWhenTheDialogOpens);
    }

    /// <summary>A menu activator's accessible name names its target, so it needs no hover tooltip.</summary>
    [Fact]
    public void ActivatorLabel_NamesTheTarget()
    {
        Assert.Equal("Actions for Platform", MenuDialogs.ActivatorLabel("Platform"));
    }

    /// <summary>The path to a folder is split into clickable segments, outermost first, each with its full path.</summary>
    [Fact]
    public void FolderBrowser_Crumbs_SplitsThePathOutermostFirst()
    {
        using TempDataDir dir = new();
        string leaf = Path.Combine(dir.Path, "alpha", "beta");
        Directory.CreateDirectory(leaf);
        string full = Path.GetFullPath(leaf);

        IReadOnlyList<BrowsedFolder> crumbs = FolderBrowser.Crumbs(full);

        Assert.Equal("beta", crumbs[^1].Name);
        Assert.Equal(full, crumbs[^1].Path);
        Assert.Equal("alpha", crumbs[^2].Name);
        Assert.Equal(Path.GetPathRoot(full), crumbs[0].Path);
        Assert.Equal(crumbs.Count, crumbs.Select(static c => c.Path).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>The top level has no path to show.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void FolderBrowser_Crumbs_TopLevelIsEmpty(string? current)
    {
        Assert.Empty(FolderBrowser.Crumbs(current));
    }

    /// <summary>The folder picker shows the path trail with the current folder last and not pressable, and draws its folders as 32px picker rows.</summary>
    [Fact]
    public async Task FolderPicker_ShowsTheTrailAndPickerRows()
    {
        await using MudBunitContext ctx = new();
        using TempDataDir dir = new();
        string start = Path.Combine(dir.Path, "alpha");
        Directory.CreateDirectory(Path.Combine(start, "inner"));
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        IDialogService dialogs = ctx.Services.GetRequiredService<IDialogService>();
        DialogParameters<FolderPickerDialog> parameters = new() { { x => x.Start, start } };

        _ = await cut.InvokeAsync(() => dialogs.ShowAsync<FolderPickerDialog>("Choose a folder", parameters, FolderPickerDialog.Options));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".folder-picker-crumb")));
        var crumbs = cut.FindAll(".folder-picker-crumb");
        Assert.Equal("alpha", crumbs[^1].TextContent.Trim());
        Assert.True(crumbs[^1].HasAttribute("disabled"));
        Assert.Equal("location", crumbs[^1].GetAttribute("aria-current"));
        Assert.Equal("inner", Assert.Single(cut.FindAll(".picker-row")).TextContent.Trim());
    }

    private static async Task OpenNameDialogAsync(MudBunitContext ctx, IRenderedComponent<ContainerFragment> cut, string title, string? location)
    {
        IDialogService dialogs = ctx.Services.GetRequiredService<IDialogService>();
        DialogParameters<NameDialog> parameters = new()
        {
            { x => x.Label, "Name" },
            { x => x.ConfirmText, "Create" },
            { x => x.Location, location },
            { x => x.SubmitAsync, static _ => Task.FromResult<string?>(null) },
        };

        _ = await cut.InvokeAsync(() => dialogs.ShowAsync<NameDialog>(title, parameters, HuddleDialogOptions.SmallForm));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));
    }
}
