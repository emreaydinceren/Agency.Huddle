using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Tests.Acp.Tools;
using TestContext = Xunit.TestContext;

namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>
/// Pins <see cref="FieldsDialog"/>, the dialog behind "Fields…" in the Tasks page's ⋮ menu, which edits a
/// saved View's fields exactly as the View editor's Fields section used to: add from a menu, switch a row
/// off and on, move it up or down or by dragging, and remove it - all of it written to the View only on Save.
/// </summary>
public sealed class FieldsDialogTests
{
    /// <summary>The Add field menu offers fields the View does not show - sprint among them - and neither the always-shown id and title nor a field already shown.</summary>
    [Fact]
    public async Task AddFieldMenu_OffersOnlyFieldsTheViewDoesNotShow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(NewView(["status", "priority"]));

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = await OpenDialogAsync(ctx);

        await root.InvokeAsync(() => root.Find(".view-editor-fields-add button").ClickAsync());
        root.WaitForAssertion(() => Assert.NotEmpty(root.FindAll(".view-editor-fields-add-option")));

        string[] offered = [.. root.FindAll(".view-editor-fields-add-option").Select(option => option.TextContent.Trim())];
        Assert.Contains("sprint", offered);
        Assert.Contains("assignee", offered);
        Assert.DoesNotContain("status", offered);
        Assert.DoesNotContain("priority", offered);
        Assert.DoesNotContain("id", offered);
        Assert.DoesNotContain("title", offered);
    }

    /// <summary>Choosing a field from the Add field menu adds it to the end of the list, and Save writes it to the View.</summary>
    [Fact]
    public async Task AddField_AppendsToTheEnd_AndSaveWritesIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(NewView(["status"]));

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = await OpenDialogAsync(ctx);

        await root.InvokeAsync(() => root.Find(".view-editor-fields-add button").ClickAsync());
        root.WaitForAssertion(() => Assert.Single(root.FindAll(".view-editor-fields-add-option"), option => string.Equals(option.TextContent.Trim(), "priority", StringComparison.Ordinal)));
        await root.InvokeAsync(() => root.FindAll(".view-editor-fields-add-option").Single(option => string.Equals(option.TextContent.Trim(), "priority", StringComparison.Ordinal)).ClickAsync());
        await SaveAsync(root);

        Assert.Equal(["status", "priority"], harness.Views.Get("v1")?.Fields);
    }

    /// <summary>Dragging a row updates the order (read from <c>ItemDropped</c>'s <c>IndexInZone</c>), persisted on Save.</summary>
    [Fact]
    public async Task ReorderByDrop_UpdatesOrder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(NewView(["status", "priority", "assignee"]));

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = await OpenDialogAsync(ctx);

        var container = root.FindComponent<MudDropContainer<string>>();
        await root.InvokeAsync(() => container.Instance.ItemDropped.InvokeAsync(new MudItemDropInfo<string>("assignee", "fields", 0)));
        await SaveAsync(root);

        Assert.Equal(["assignee", "status", "priority"], harness.Views.Get("v1")?.Fields);
    }

    /// <summary>The up and down icon buttons on a row are a keyboard alternative to dragging.</summary>
    [Fact]
    public async Task ReorderByUpDownButtons_UpdatesOrder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(NewView(["status", "priority"]));

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = await OpenDialogAsync(ctx);

        await root.InvokeAsync(() => root.Find(".view-editor-field-down-status").ClickAsync());
        await SaveAsync(root);

        Assert.Equal(["priority", "status"], harness.Views.Get("v1")?.Fields);
    }

    /// <summary>A row's remove button drops that field, and the list repaints - a parent render alone leaves <c>MudDropContainer</c> showing stale rows.</summary>
    [Fact]
    public async Task RemoveButton_RemovesTheField_AndRepaintsTheRows()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(NewView(["status", "priority"]));

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = await OpenDialogAsync(ctx);
        Assert.Equal(2, root.FindAll(".view-editor-field-row").Count);

        await root.InvokeAsync(() => root.Find(".view-editor-field-remove-priority").ClickAsync());

        Assert.Single(root.FindAll(".view-editor-field-row"));
        Assert.Empty(root.FindAll(".view-editor-field-remove-priority"));
        await SaveAsync(root);
        Assert.Equal(["status"], harness.Views.Get("v1")?.Fields);
    }

    /// <summary>Switching a row off and saving excludes it from the saved fields, without disturbing the order of the fields still on.</summary>
    [Fact]
    public async Task SwitchOff_ExcludesTheFieldOnSave_KeepsOtherOrder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(NewView(["status", "priority", "assignee"]));

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = await OpenDialogAsync(ctx);

        var priority = root.FindComponents<MudSwitch<bool>>().Single(HasSwitchClass("priority"));
        await root.InvokeAsync(() => priority.Instance.ValueChanged.InvokeAsync(false));
        await SaveAsync(root);

        Assert.Equal(["status", "assignee"], harness.Views.Get("v1")?.Fields);
    }

    /// <summary>Switching a row off, then back on, and saving restores it at its original position rather than at the end.</summary>
    [Fact]
    public async Task SwitchOffThenOn_RestoresItsOriginalPosition()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(NewView(["status", "priority", "assignee"]));

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = await OpenDialogAsync(ctx);

        var priority = root.FindComponents<MudSwitch<bool>>().Single(HasSwitchClass("priority"));
        await root.InvokeAsync(() => priority.Instance.ValueChanged.InvokeAsync(false));
        priority = root.FindComponents<MudSwitch<bool>>().Single(HasSwitchClass("priority"));
        await root.InvokeAsync(() => priority.Instance.ValueChanged.InvokeAsync(true));
        await SaveAsync(root);

        Assert.Equal(["status", "priority", "assignee"], harness.Views.Get("v1")?.Fields);
    }

    /// <summary>Cancel closes the dialog without touching the View.</summary>
    [Fact]
    public async Task Cancel_LeavesTheViewUnchanged()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(NewView(["status", "priority"]));

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> root = await OpenDialogAsync(ctx);

        await root.InvokeAsync(() => root.Find(".view-editor-field-remove-status").ClickAsync());
        await root.InvokeAsync(() => root.Find(".fields-dialog-cancel").ClickAsync());

        Assert.Equal(["status", "priority"], harness.Views.Get("v1")?.Fields);
    }

    /// <summary>A List View named <c>v1</c> showing <paramref name="fields"/>.</summary>
    /// <param name="fields">The field keys, in order.</param>
    private static TaskView NewView(IReadOnlyList<string> fields) => new() { Id = "v1", Name = "v1", Kind = ViewKind.List, Fields = fields };

    /// <summary>Returns a predicate matching the row switch for <paramref name="key"/> (<c>.view-editor-field-switch-{key}</c>).</summary>
    /// <param name="key">The field key.</param>
    private static Func<IRenderedComponent<MudSwitch<bool>>, bool> HasSwitchClass(string key) =>
        component => string.Equals(component.Instance.Class, $"view-editor-field-switch-{key}", StringComparison.Ordinal);

    /// <summary>Clicks Save inside the dispatch, never holding the element across an await.</summary>
    /// <param name="root">The rendered dialog host.</param>
    private static Task SaveAsync(IRenderedComponent<ContainerFragment> root) =>
        root.InvokeAsync(() => root.Find(".fields-dialog-save").ClickAsync());

    /// <summary>A context holding the harness's services, so the dialog can reach <see cref="ViewStore"/>.</summary>
    /// <param name="harness">Supplies the Task and View stores.</param>
    private static MudBunitContext NewContext(TaskToolHarness harness)
    {
        MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);
        return ctx;
    }

    /// <summary>Opens <see cref="FieldsDialog"/> for the View <c>v1</c> through <see cref="IDialogService"/>, beside the hosts it needs, and waits for it to paint.</summary>
    /// <param name="ctx">The bUnit context.</param>
    private static async Task<IRenderedComponent<ContainerFragment>> OpenDialogAsync(MudBunitContext ctx)
    {
        IRenderedComponent<ContainerFragment> root = ctx.RenderWithPopovers(static _ => { });
        IDialogService dialogs = ctx.Services.GetRequiredService<IDialogService>();
        DialogParameters<FieldsDialog> parameters = new() { { dialog => dialog.ViewId, "v1" } };

        _ = await root.InvokeAsync(() => dialogs.ShowAsync<FieldsDialog>("Fields", parameters));

        root.WaitForAssertion(() => Assert.NotEmpty(root.FindAll(".view-editor-field-row")));
        return root;
    }
}
