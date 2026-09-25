using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins the Blazor side of the <c>#</c> Task picker in <see cref="Composer"/> (Spec §13.13.4,
/// corrections-B6 "D15.3"): the three <c>[JSInvokable]</c> methods
/// (<c>TaskQueryAsync</c>, <c>MoveAsync</c>, <c>PickAsync</c>), the accessibility contract between
/// the textarea and the rendered list, the "no matches" row and a mouse click's JavaScript call. The
/// JavaScript side (<c>app.js</c>'s <c>teamComposer</c> keydown handling) is Task 15.3.i's job - this
/// file exercises Blazor only, through <c>FindComponent&lt;Composer&gt;().Instance</c> via
/// <c>cut.InvokeAsync</c>, exactly as corrections-B6 "D15.3" item 5 directs.
/// </summary>
public sealed class ComposerTests
{
    /// <summary>A query that matches a Task by title opens the popover and renders only the matching row (Spec §13.13.4 "the search").</summary>
    [Fact]
    public async Task TaskQueryAsync_MatchingQuery_OpensListboxWithTheMatchingRow()
    {
        using TaskToolHarness harness = new();
        ComposerTests.SeedTask(harness, "PLAT-0001", "Sample import", "2026-01-01T00:00:00Z");
        ComposerTests.SeedTask(harness, "PLAT-0002", "Write specs", "2026-01-02T00:00:00Z");

        await using MudBunitContext ctx = ComposerTests.NewContext(harness);
        IRenderedComponent<ContainerFragment> cut = ComposerTests.RenderComposer(ctx);
        IRenderedComponent<Composer> composer = cut.FindComponent<Composer>();

        await cut.InvokeAsync(() => composer.Instance.TaskQueryAsync("sa"));

        Assert.Single(cut.FindAll("[role='listbox']"));
        Assert.Single(cut.FindAll("[role='option']"));
        Assert.Equal("PLAT-0001", cut.Find(".composer-picker-id").TextContent.Trim());
    }

    /// <summary>Each matching row shows the Task's id, title and status word (Spec §13.13.4: id, title, and "a status MudChip").</summary>
    [Fact]
    public async Task TaskQueryAsync_MatchingQuery_RowShowsIdTitleAndStatus()
    {
        using TaskToolHarness harness = new();
        ComposerTests.SeedTask(harness, "PLAT-0001", "Sample import", "2026-01-01T00:00:00Z", TaskState.InProgress);

        await using MudBunitContext ctx = ComposerTests.NewContext(harness);
        IRenderedComponent<ContainerFragment> cut = ComposerTests.RenderComposer(ctx);
        IRenderedComponent<Composer> composer = cut.FindComponent<Composer>();

        await cut.InvokeAsync(() => composer.Instance.TaskQueryAsync("sa"));

        Assert.Equal("PLAT-0001", cut.Find(".composer-picker-id").TextContent.Trim());
        Assert.Equal("Sample import", cut.Find(".composer-picker-title").TextContent.Trim());
        Assert.Equal(TaskStates.ToWire(TaskState.InProgress), cut.Find(".composer-picker-status").TextContent.Trim());
    }

    /// <summary><c>TaskQueryAsync(null)</c> closes the picker: the list disappears and the textarea reports <c>aria-expanded="false"</c> (Spec §13.13.4).</summary>
    [Fact]
    public async Task TaskQueryAsync_Null_ClosesTheListboxAndSetsAriaExpandedFalse()
    {
        using TaskToolHarness harness = new();
        ComposerTests.SeedTask(harness, "PLAT-0001", "Sample import", "2026-01-01T00:00:00Z");

        await using MudBunitContext ctx = ComposerTests.NewContext(harness);
        IRenderedComponent<ContainerFragment> cut = ComposerTests.RenderComposer(ctx);
        IRenderedComponent<Composer> composer = cut.FindComponent<Composer>();

        await cut.InvokeAsync(() => composer.Instance.TaskQueryAsync("sa"));
        Assert.Single(cut.FindAll("[role='listbox']"));

        await cut.InvokeAsync(() => composer.Instance.TaskQueryAsync(null));

        Assert.Empty(cut.FindAll("[role='listbox']"));
        Assert.Equal("false", cut.Find("textarea").GetAttribute("aria-expanded"));
    }

    /// <summary>While the picker is open, the textarea's <c>aria-expanded</c>, <c>aria-controls</c> and <c>aria-activedescendant</c> point at the rendered list and its highlighted row (Spec §13.13.4 "Accessibility").</summary>
    [Fact]
    public async Task TaskQueryAsync_MatchingQuery_TextareaAriaAttributesReferenceTheListAndHighlight()
    {
        using TaskToolHarness harness = new();
        ComposerTests.SeedTask(harness, "PLAT-0001", "Sample import", "2026-01-01T00:00:00Z");

        await using MudBunitContext ctx = ComposerTests.NewContext(harness);
        IRenderedComponent<ContainerFragment> cut = ComposerTests.RenderComposer(ctx);
        IRenderedComponent<Composer> composer = cut.FindComponent<Composer>();

        await cut.InvokeAsync(() => composer.Instance.TaskQueryAsync("sa"));

        var textarea = cut.Find("textarea");
        var listbox = cut.Find("[role='listbox']");
        var highlighted = cut.Find("[aria-selected='true']");

        Assert.Equal("true", textarea.GetAttribute("aria-expanded"));
        Assert.Equal(listbox.GetAttribute("id"), textarea.GetAttribute("aria-controls"));
        Assert.Equal(highlighted.GetAttribute("id"), textarea.GetAttribute("aria-activedescendant"));
    }

    /// <summary><c>MoveAsync(-1)</c> from the first (default-highlighted) row wraps the highlight to the last one (Spec §13.13.4 "moves the highlight, wrapping around").</summary>
    [Fact]
    public async Task MoveAsync_NegativeFromTheFirstRow_WrapsToTheLastRow()
    {
        using TaskToolHarness harness = new();
        ComposerTests.SeedTask(harness, "PLAT-0001", "Newest", "2026-01-03T00:00:00Z");
        ComposerTests.SeedTask(harness, "PLAT-0002", "Middle", "2026-01-02T00:00:00Z");
        ComposerTests.SeedTask(harness, "PLAT-0003", "Oldest", "2026-01-01T00:00:00Z");

        await using MudBunitContext ctx = ComposerTests.NewContext(harness);
        IRenderedComponent<ContainerFragment> cut = ComposerTests.RenderComposer(ctx);
        IRenderedComponent<Composer> composer = cut.FindComponent<Composer>();

        // An empty query returns the most recently updated Active Tasks (Spec §13.13.4 "the search"),
        // so the default highlight starts on PLAT-0001 (the newest). WaitForAssertion (not a bare
        // Assert) is defensive against StateHasChanged's render landing after cut.InvokeAsync's own
        // await returns.
        await cut.InvokeAsync(() => composer.Instance.TaskQueryAsync(""));
        cut.WaitForAssertion(() => Assert.Equal("PLAT-0001", ComposerTests.HighlightedRowId(cut)));

        await cut.InvokeAsync(() => composer.Instance.MoveAsync(-1));

        cut.WaitForAssertion(() => Assert.Equal("PLAT-0003", ComposerTests.HighlightedRowId(cut)));
    }

    /// <summary>Moving forward past the last row wraps the highlight back to the first one (Spec §13.13.4 "moves the highlight, wrapping around").</summary>
    [Fact]
    public async Task MoveAsync_PositivePastTheLastRow_WrapsToTheFirstRow()
    {
        using TaskToolHarness harness = new();
        ComposerTests.SeedTask(harness, "PLAT-0001", "Newest", "2026-01-03T00:00:00Z");
        ComposerTests.SeedTask(harness, "PLAT-0002", "Middle", "2026-01-02T00:00:00Z");
        ComposerTests.SeedTask(harness, "PLAT-0003", "Oldest", "2026-01-01T00:00:00Z");

        await using MudBunitContext ctx = ComposerTests.NewContext(harness);
        IRenderedComponent<ContainerFragment> cut = ComposerTests.RenderComposer(ctx);
        IRenderedComponent<Composer> composer = cut.FindComponent<Composer>();

        await cut.InvokeAsync(() => composer.Instance.TaskQueryAsync(""));

        await cut.InvokeAsync(() => composer.Instance.MoveAsync(1));
        await cut.InvokeAsync(() => composer.Instance.MoveAsync(1));
        cut.WaitForAssertion(() => Assert.Equal("PLAT-0003", ComposerTests.HighlightedRowId(cut)));

        await cut.InvokeAsync(() => composer.Instance.MoveAsync(1));

        cut.WaitForAssertion(() => Assert.Equal("PLAT-0001", ComposerTests.HighlightedRowId(cut)));
    }

    /// <summary><c>PickAsync</c> returns the highlighted row's id, and leaves the picker closed (corrections-B6 "D15.3" item 4: "PickAsync sets pickerOpen = false and re-renders before returning").</summary>
    [Fact]
    public async Task PickAsync_WithAHighlightedMatch_ReturnsItsIdAndClosesThePicker()
    {
        using TaskToolHarness harness = new();
        ComposerTests.SeedTask(harness, "PLAT-0001", "Sample import", "2026-01-01T00:00:00Z");

        await using MudBunitContext ctx = ComposerTests.NewContext(harness);
        IRenderedComponent<ContainerFragment> cut = ComposerTests.RenderComposer(ctx);
        IRenderedComponent<Composer> composer = cut.FindComponent<Composer>();

        await cut.InvokeAsync(() => composer.Instance.TaskQueryAsync("sa"));

        string? picked = await cut.InvokeAsync(() => composer.Instance.PickAsync());

        Assert.Equal("PLAT-0001", picked);
        Assert.Empty(cut.FindAll("[role='listbox']"));
    }

    /// <summary><c>PickAsync</c> returns <see langword="null"/> when there are no matches, so a stray Enter never sends (Spec §13.13.4).</summary>
    [Fact]
    public async Task PickAsync_WithNoMatches_ReturnsNull()
    {
        using TaskToolHarness harness = new();
        ComposerTests.SeedTask(harness, "PLAT-0001", "Sample import", "2026-01-01T00:00:00Z");

        await using MudBunitContext ctx = ComposerTests.NewContext(harness);
        IRenderedComponent<ContainerFragment> cut = ComposerTests.RenderComposer(ctx);
        IRenderedComponent<Composer> composer = cut.FindComponent<Composer>();

        await cut.InvokeAsync(() => composer.Instance.TaskQueryAsync("zzz"));

        string? picked = await cut.InvokeAsync(() => composer.Instance.PickAsync());

        Assert.Null(picked);
    }

    /// <summary>With no matches, the list shows exactly one disabled row with the exact Spec §13.13.4 text.</summary>
    [Fact]
    public async Task TaskQueryAsync_WithNoMatches_ShowsOneDisabledRowWithTheExactMessage()
    {
        using TaskToolHarness harness = new();
        ComposerTests.SeedTask(harness, "PLAT-0001", "Sample import", "2026-01-01T00:00:00Z");

        await using MudBunitContext ctx = ComposerTests.NewContext(harness);
        IRenderedComponent<ContainerFragment> cut = ComposerTests.RenderComposer(ctx);
        IRenderedComponent<Composer> composer = cut.FindComponent<Composer>();

        await cut.InvokeAsync(() => composer.Instance.TaskQueryAsync("zzz"));

        var rows = cut.FindAll("[role='option']");
        Assert.Single(rows);
        Assert.Equal("No task matches '#zzz'", rows[0].TextContent.Trim());
    }

    /// <summary>A mouse click on a matching row calls <c>teamComposer.insertTask</c> with the row's id (Spec §13.13.4 "A mouse click").</summary>
    [Fact]
    public async Task Click_OnAMatchingRow_InvokesInsertTaskWithTheTaskId()
    {
        using TaskToolHarness harness = new();
        ComposerTests.SeedTask(harness, "PLAT-0001", "Sample import", "2026-01-01T00:00:00Z");

        await using MudBunitContext ctx = ComposerTests.NewContext(harness);
        IRenderedComponent<ContainerFragment> cut = ComposerTests.RenderComposer(ctx);
        IRenderedComponent<Composer> composer = cut.FindComponent<Composer>();

        await cut.InvokeAsync(() => composer.Instance.TaskQueryAsync("sa"));

        cut.Find("[role='option']").Click();

        var invocation = ctx.JSInterop.VerifyInvoke("teamComposer.insertTask");
        Assert.Equal("PLAT-0001", invocation.Arguments[1]);
    }

    /// <summary>
    /// With <c>Team:Tasks:Enabled</c> false, the picker never opens - it offers nothing to query
    /// against (corrections-B6 "D15.1" item 9: "no links (null resolver path) and no # picker; one
    /// test each" - this is the picker's test).
    /// </summary>
    [Fact]
    public async Task TaskQueryAsync_TasksDisabled_NeverOpensThePicker()
    {
        using TaskToolHarness harness = new();
        ComposerTests.SeedTask(harness, "PLAT-0001", "Sample import", "2026-01-01T00:00:00Z");

        await using MudBunitContext ctx = ComposerTests.NewContext(harness, tasksEnabled: false);
        IRenderedComponent<ContainerFragment> cut = ComposerTests.RenderComposer(ctx);
        IRenderedComponent<Composer> composer = cut.FindComponent<Composer>();

        await cut.InvokeAsync(() => composer.Instance.TaskQueryAsync("sa"));

        Assert.Empty(cut.FindAll("[role='listbox']"));
    }

    /// <summary>The id shown in the picker row currently marked <c>aria-selected="true"</c>, read from its <c>.composer-picker-id</c> element rather than the whole row's text.</summary>
    private static string HighlightedRowId(IRenderedComponent<ContainerFragment> cut) =>
        cut.Find("[aria-selected='true'] .composer-picker-id").TextContent.Trim();

    /// <summary>
    /// Writes a Task file under the harness's Tasks root through <see cref="TaskToolHarness.SeedOnDisk"/>,
    /// giving each fixture its own exact <see cref="TaskItem.Updated"/> (corrections-B6 "D15.3" item 7).
    /// <see cref="TaskToolHarness.SeedOnDisk"/> (not a raw <c>TestTaskStore.WriteTask</c> write) is what
    /// keeps that value from being overwritten by an "edited outside Huddle" Change log entry - see its
    /// own summary (R8 facts "Harness trap").
    /// </summary>
    private static void SeedTask(TaskToolHarness harness, string id, string title, string updatedAt, TaskState status = TaskState.ToDo)
    {
        TaskItem task = TestTasks.Make(
            id: id,
            title: title,
            status: status,
            changeLog: [TestTasks.Entry(updatedAt, "You", "Created")]);
        harness.SeedOnDisk(task);
    }

    /// <summary>Renders a bare <see cref="Composer"/> the same way <c>ChatPageTests.Composer_StatusLine_DoesNotFollowARoomSwitch</c> does, through <see cref="MudBunitContext.RenderWithPopovers"/> so a floating list renders in the same subtree (corrections-B6 "D15.3" item 5).</summary>
    private static IRenderedComponent<ContainerFragment> RenderComposer(MudBunitContext ctx) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<Composer>(0);
            builder.AddAttribute(1, nameof(Composer.RoomId), "any-room");
            builder.CloseComponent();
        });

    /// <summary>
    /// A fresh <see cref="MudBunitContext"/> with the harness's <c>ChatService</c> and its
    /// <see cref="TaskStore"/> registered - the bare <c>Composer</c> context corrections-B6 "D15.1"
    /// item 1 describes, extended with Tasks services for 15.3.
    /// </summary>
    /// <param name="harness">Supplies <c>ChatService</c> and <c>TaskStore</c> (Composer's dependencies).</param>
    /// <param name="tasksEnabled">The registered <c>Team:Tasks:Enabled</c> value.</param>
    private static MudBunitContext NewContext(TaskToolHarness harness, bool tasksEnabled = true)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(harness.Chat);
        ctx.Services.AddSingleton(harness.Store);
        ctx.Services.AddSingleton(harness.Events);
        TeamOptions options = new() { Tasks = new TasksOptions { Enabled = tasksEnabled } };
        ctx.Services.AddSingleton<IOptions<TeamOptions>>(Options.Create(options));
        return ctx;
    }
}
