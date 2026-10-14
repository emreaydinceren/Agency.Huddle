using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Tests.Acp.Tools;
using TestContext = Xunit.TestContext;

namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>
/// Pins <see cref="ViewActions.RenameAsync"/>, the "Rename" dialog both ⋮ menus open: the shared name dialog,
/// prefilled with the View's name, validated live against the same rules as <c>ViewValidator</c>, saving
/// through <see cref="ViewStore"/> and leaving every other part of the View alone.
/// </summary>
public sealed class RenameViewTests
{
    private const string Input = ".mud-dialog-content input";
    private const string Actions = ".mud-dialog-actions button";

    /// <summary>The dialog is titled "Rename view", labelled "Name", prefilled with the View's name, and its primary reads "Rename".</summary>
    [Fact]
    public async Task Dialog_IsTitledRenameView_AndPrefilledWithTheName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(new TaskView { Id = "v1", Name = "Platform board", Kind = ViewKind.List });

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = await OpenAsync(ctx, "v1");

        Assert.Equal("Rename view", root.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal("Name", root.Find(".mud-dialog-content label").TextContent.Trim());
        Assert.Equal("Platform board", root.Find(Input).GetAttribute("value"));
        Assert.Equal("Rename", root.FindAll(Actions)[1].TextContent.Trim());
    }

    /// <summary>Typing a new name and confirming saves it - trimmed - and leaves the View's other settings as they were.</summary>
    [Fact]
    public async Task Rename_SavesTheTrimmedName_AndKeepsTheRestOfTheView()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(new TaskView { Id = "v1", Name = "Platform board", Kind = ViewKind.List, Dense = false, Fields = ["status"] });

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = await OpenAsync(ctx, "v1");

        await TypeAsync(root, "  Sprint review  ");
        await root.InvokeAsync(() => root.FindAll(Actions)[1].ClickAsync());

        TaskView? renamed = harness.Views.Get("v1");
        Assert.NotNull(renamed);
        Assert.Equal("Sprint review", renamed.Name);
        Assert.False(renamed.Dense);
        Assert.Equal(["status"], renamed.Fields);
    }

    /// <summary>A name another View already has - ignoring case - is refused on the field and the primary is disabled; the View's own current name is fine.</summary>
    [Fact]
    public async Task DuplicateName_IsRefused_ButItsOwnNameIsNot()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(new TaskView { Id = "v1", Name = "Platform board", Kind = ViewKind.List });
        _ = harness.Views.Save(new TaskView { Id = "v2", Name = "Support urgent", Kind = ViewKind.List });

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = await OpenAsync(ctx, "v1");

        await TypeAsync(root, "support URGENT");
        Assert.Equal("Another View already has this name.", root.Find(".mud-input-helper-text").TextContent.Trim());
        Assert.True(root.FindAll(Actions)[1].HasAttribute("disabled"));

        await TypeAsync(root, "platform BOARD");
        Assert.False(root.FindAll(Actions)[1].HasAttribute("disabled"));
    }

    /// <summary>A name over 60 characters is refused on the field, as <c>ViewValidator</c> would.</summary>
    [Fact]
    public async Task OverlongName_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(new TaskView { Id = "v1", Name = "Platform board", Kind = ViewKind.List });

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = await OpenAsync(ctx, "v1");

        await TypeAsync(root, new string('x', 61));

        Assert.Equal("Name must be 1-60 characters.", root.Find(".mud-input-helper-text").TextContent.Trim());
        Assert.True(root.FindAll(Actions)[1].HasAttribute("disabled"));
    }

    /// <summary>Cancel closes the dialog and the View keeps its name.</summary>
    [Fact]
    public async Task Cancel_KeepsTheName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(new TaskView { Id = "v1", Name = "Platform board", Kind = ViewKind.List });

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = await OpenAsync(ctx, "v1");

        await TypeAsync(root, "Something else");
        await root.InvokeAsync(() => root.FindAll(Actions)[0].ClickAsync());

        Assert.Equal("Platform board", harness.Views.Get("v1")?.Name);
    }

    /// <summary>Types into the field the way a user does, so each keystroke re-validates.</summary>
    /// <param name="root">The rendered dialog host.</param>
    /// <param name="text">The text to type.</param>
    private static Task TypeAsync(IRenderedComponent<ContainerFragment> root, string text) =>
        root.InvokeAsync(() => root.Find(Input).InputAsync(new ChangeEventArgs { Value = text }));

    /// <summary>A context holding the harness's services, so the dialog can reach <see cref="ViewStore"/>.</summary>
    /// <param name="harness">Supplies the Task and View stores.</param>
    private static MudBunitContext NewContext(TaskToolHarness harness)
    {
        MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);
        return ctx;
    }

    /// <summary>Opens the rename dialog for <paramref name="viewId"/>, beside the hosts it needs, and waits for it to paint.</summary>
    /// <param name="ctx">The bUnit context.</param>
    /// <param name="viewId">The View to rename.</param>
    private static async Task<IRenderedComponent<ContainerFragment>> OpenAsync(MudBunitContext ctx, string viewId)
    {
        IRenderedComponent<ContainerFragment> root = ctx.RenderWithPopovers(static _ => { });
        IDialogService dialogs = ctx.Services.GetRequiredService<IDialogService>();
        ViewStore store = ctx.Services.GetRequiredService<ViewStore>();

        await root.InvokeAsync(() => ViewActions.RenameAsync(dialogs, store, viewId));

        root.WaitForAssertion(() => Assert.NotEmpty(root.FindAll(Input)));
        return root;
    }
}
