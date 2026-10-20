namespace Agency.Huddle.Tests.Ui.Tasks;

using Bunit;
using Bunit.Rendering;
using Agency.Huddle.App.Components.Library;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Pins the Library's Markdown toolbar on a Task's Description (<see cref="TaskDetail"/>, read view spec D-7): it shows above the field
/// while the Description is in Edit, without the <c>[[ ]]</c> button, is gone in Preview, and is wired to the field's textarea by
/// <c>markdown-textarea.js</c>. The Library's own toolbar keeps its <c>[[ ]]</c> button.
/// </summary>
public sealed class TaskDetailDescriptionToolbarTests
{
    /// <summary>Creates a Task through the real service.</summary>
    private static TaskItem CreateTask(TaskToolHarness harness) =>
        Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Described", "Platform", null, Description: "Some text."), TaskActors.Human(harness.Options.Value))).Task;

    /// <summary>In Edit the Description has the toolbar with six buttons and no <c>[[ ]]</c> button, ahead of the field.</summary>
    [Fact]
    public async Task Edit_ShowsTheToolbarWithoutTheWikilinkButton()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = TaskDetailFieldsTestSupport.RenderDetail(ctx, task.Id);

        Assert.Equal(
            [
                MarkdownToolbar.Commands.Bold,
                MarkdownToolbar.Commands.Italic,
                MarkdownToolbar.Commands.Heading,
                MarkdownToolbar.Commands.Code,
                MarkdownToolbar.Commands.BulletList,
                MarkdownToolbar.Commands.TaskList,
            ],
            cut.FindAll(".task-detail-description-editor [data-md-command]").Select(static b => b.GetAttribute("data-md-command") ?? string.Empty));
        Assert.Empty(cut.FindAll(".task-detail-description-editor .markdown-toolbar-wikilink"));
        AngleSharp.Dom.IElement editor = cut.Find(".task-detail-description-editor");
        Assert.Equal("markdown-toolbar", editor.FirstElementChild?.ClassName?.Split(' ')[0]);
        Assert.NotNull(editor.QuerySelector("textarea"));
    }

    /// <summary>In Preview the toolbar is gone with the field.</summary>
    [Fact]
    public async Task Preview_ShowsNoToolbar()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = TaskDetailFieldsTestSupport.RenderDetail(ctx, task.Id);

        await cut.InvokeAsync(() => TaskDetailFieldsTestSupport.FindButton(cut, "Preview").ClickAsync());

        Assert.Empty(cut.FindAll(".task-detail-description-editor"));
        Assert.Empty(cut.FindAll("[data-md-toolbar]"));
    }

    /// <summary>A toolbar given no parameters still has the <c>[[ ]]</c> button: the Library is unchanged.</summary>
    [Fact]
    public async Task MarkdownToolbar_DefaultParameters_StillHasTheWikilinkButton()
    {
        await using MudBunitContext context = new();

        IRenderedComponent<MarkdownToolbar> cut = context.Render<MarkdownToolbar>();

        Assert.Single(cut.FindAll(".markdown-toolbar-wikilink"));
        Assert.Equal(7, cut.FindAll("[data-md-command]").Count);
    }

    /// <summary><c>ShowWikilink="false"</c> removes only that button.</summary>
    [Fact]
    public async Task MarkdownToolbar_ShowWikilinkOff_RemovesOnlyThatButton()
    {
        await using MudBunitContext context = new();

        IRenderedComponent<MarkdownToolbar> cut = context.Render<MarkdownToolbar>(parameters => parameters.Add(static toolbar => toolbar.ShowWikilink, false));

        Assert.Empty(cut.FindAll(".markdown-toolbar-wikilink"));
        Assert.Equal(6, cut.FindAll("[data-md-command]").Count);
    }

    /// <summary>Once the field is on the page, the component asks <c>markdown-textarea.js</c> to wire the toolbar to the textarea in its wrapper.</summary>
    [Fact]
    public async Task Edit_AsksTheScriptToAttachTheToolbar()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        BunitJSModuleInterop module = ctx.JSInterop.SetupModule("./markdown-textarea.js");
        BunitJSModuleInterop handle = module.SetupModule("attachIn", _ => true);
        handle.SetupVoid("dispose", _ => true).SetVoidResult();

        IRenderedComponent<ContainerFragment> cut = TaskDetailFieldsTestSupport.RenderDetail(ctx, task.Id);

        cut.WaitForAssertion(() => Assert.Single(module.Invocations["attachIn"]));
    }
}
