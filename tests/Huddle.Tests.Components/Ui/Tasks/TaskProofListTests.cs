namespace Agency.Huddle.Tests.Ui.Tasks;

using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Tasks;
using static Agency.Huddle.Tests.Tasks.TaskTriggerTestSupport;
using HumanGateCopy = Agency.Huddle.App.Components.Tasks.HumanGateCopy;
using TaskProofListComponent = Agency.Huddle.App.Components.Tasks.TaskProofList;

/// <summary>
/// Pins T9.1 (Spec §6.8, corrections P9a, UI copy rows 9-13): the Proof checklist of the Task panel,
/// <c>TaskProofList</c>, and its <c>Ask</c> button, which posts the Human's message to the item's
/// owner through <see cref="TaskTriggerService.AskAsync"/>.
/// </summary>
public sealed class TaskProofListTests
{
    /// <summary>An open item owned by an Agent.</summary>
    private static readonly ProofItem OpenOwned = new("citation check", false, "Nova", "19 of 23 in #RSH-209", ProofItemSource.Process);

    /// <summary>A met item.</summary>
    private static readonly ProofItem MetItem = new("draft written", true, null, null, ProofItemSource.Process);

    /// <summary>An open item nobody can be named for.</summary>
    private static readonly ProofItem OpenUnowned = new("legal review", false, null, "no owner yet", ProofItemSource.Gate);

    /// <summary>An open item owned by the Human.</summary>
    private static readonly ProofItem OpenHumanOwned = new("final read", false, "Emre", null, ProofItemSource.Process);

    /// <summary>Every item is one row, in order, marked met or open and carrying its label, owner and detail.</summary>
    [Fact]
    public async Task Rows_OnePerItem_ShowMarkLabelOwnerAndDetail()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "Emre" }, ct);
        await using MudBunitContext ctx = NewContext(harness);
        TaskItem task = TestTasks.Make(id: "PLAT-0001");

        IRenderedComponent<ContainerFragment> cut = Render(ctx, task, new Proof([MetItem, OpenOwned]), "Emre");

        IReadOnlyList<AngleSharp.Dom.IElement> rows = cut.FindAll(".task-proof-row");
        Assert.Equal(2, rows.Count);
        Assert.Equal("true", rows[0].GetAttribute("data-met"));
        Assert.Equal("false", rows[1].GetAttribute("data-met"));
        Assert.Equal("draft written", rows[0].QuerySelector(".task-proof-label")?.TextContent.Trim());
        Assert.Equal("citation check", rows[1].QuerySelector(".task-proof-label")?.TextContent.Trim());
        Assert.Equal("Nova", rows[1].QuerySelector(".task-proof-owner")?.TextContent.Trim());
        Assert.Equal("19 of 23 in #RSH-209", rows[1].QuerySelector(".task-proof-detail")?.TextContent.Trim());
        Assert.NotNull(rows[0].QuerySelector(".task-proof-mark-met"));
        Assert.NotNull(rows[1].QuerySelector(".task-proof-mark-open"));
    }

    /// <summary>An ordinary Task's heading reads "Definition of Done (for information)" (row 9).</summary>
    [Fact]
    public async Task Heading_OrdinaryTask_ReadsDefinitionOfDoneForInformation()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "Emre" }, ct);
        await using MudBunitContext ctx = NewContext(harness);

        IRenderedComponent<ContainerFragment> cut = Render(ctx, TestTasks.Make(), new Proof([OpenOwned]), "Emre");

        Assert.Equal("Definition of Done (for information)", cut.Find(".task-proof-heading").TextContent.Trim());
        Assert.Equal("Definition of Done (for information)", HumanGateCopy.ProofHeadingUngated);
    }

    /// <summary>A gated Task's heading reads "Proof" (row 10).</summary>
    [Fact]
    public async Task Heading_GatedTask_ReadsProof()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "Emre" }, ct);
        await using MudBunitContext ctx = NewContext(harness);
        TaskItem gated = TestTasks.Make() with { HumanGate = true };

        IRenderedComponent<ContainerFragment> cut = Render(ctx, gated, new Proof([OpenOwned]), "Emre");

        Assert.Equal("Proof", cut.Find(".task-proof-heading").TextContent.Trim());
        Assert.Equal("Proof", HumanGateCopy.ProofHeadingGated);
    }

    /// <summary>A Proof with no items renders nothing at all.</summary>
    [Fact]
    public async Task Render_EmptyProof_RendersNothing()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "Emre" }, ct);
        await using MudBunitContext ctx = NewContext(harness);

        IRenderedComponent<ContainerFragment> cut = Render(ctx, TestTasks.Make(), Proof.Empty, "Emre");

        Assert.Empty(cut.FindAll(".task-proof-list"));
        Assert.Empty(cut.FindAll(".task-proof-heading"));
        Assert.Empty(cut.FindAll(".task-proof-row"));
    }

    /// <summary>Ask shows only on an open item with an owner who is not the Human: not on a met item, an unowned one or the Human's own.</summary>
    [Fact]
    public async Task Ask_ShownOnlyOnOpenItemOwnedByAnotherAgent()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "Emre" }, ct);
        await using MudBunitContext ctx = NewContext(harness);
        Proof proof = new([MetItem, OpenOwned, OpenUnowned, OpenHumanOwned]);

        IRenderedComponent<ContainerFragment> cut = Render(ctx, TestTasks.Make(), proof, "Emre");

        IReadOnlyList<AngleSharp.Dom.IElement> rows = cut.FindAll(".task-proof-row");
        Assert.Equal(4, rows.Count);
        Assert.Null(rows[0].QuerySelector("button.task-proof-ask"));
        Assert.NotNull(rows[1].QuerySelector("button.task-proof-ask"));
        Assert.Null(rows[2].QuerySelector("button.task-proof-ask"));
        Assert.Null(rows[3].QuerySelector("button.task-proof-ask"));
        Assert.Single(cut.FindAll("button.task-proof-ask"));
    }

    /// <summary>The button reads "Ask Nova" and its accessible name adds the item: "Ask Nova for citation check" (row 11).</summary>
    [Fact]
    public async Task Ask_ButtonTextAndAccessibleName_FollowRowEleven()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "Emre" }, ct);
        await using MudBunitContext ctx = NewContext(harness);

        IRenderedComponent<ContainerFragment> cut = Render(ctx, TestTasks.Make(), new Proof([OpenOwned]), "Emre");

        AngleSharp.Dom.IElement button = cut.Find("button.task-proof-ask");
        Assert.Equal("Ask Nova", button.TextContent.Trim());
        Assert.Equal("Ask Nova for citation check", button.GetAttribute("aria-label"));
        Assert.Equal("Ask Nova", HumanGateCopy.AskButton("Nova"));
        Assert.Equal("Ask Nova for citation check", HumanGateCopy.AskAccessibleName("Nova", "citation check"));
    }

    /// <summary>Clicking Ask posts row 12's exact text from the Human into the Task's Room and shows the success snackbar (row 13).</summary>
    [Fact]
    public async Task Ask_Clicked_PostsTheExactTextAndShowsTheSuccessSnackbar()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "Emre" }, ct);
        Cast cast = await StartWakingAsync(harness, ct, start: false);
        TaskItem task = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Kai", originRoomId: cast.Room.Id));
        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> cut = Render(ctx, task, new Proof([OpenOwned]), "Emre");

        cut.Find("button.task-proof-ask").Click();

        cut.WaitForAssertion(() => Assert.Single(harness.Posts), TimeSpan.FromSeconds(5));
        MessagePostedEvent post = Assert.Single(harness.Posts);
        Assert.Equal("@Nova, PLAT-0001 needs citation check before it can come to me.", post.Message.Text);
        Assert.Equal("@Nova, PLAT-0001 needs citation check before it can come to me.", HumanGateCopy.AskMessage("Nova", "PLAT-0001", "citation check"));
        Assert.Equal(KnownIds.Human, post.Message.SenderId);
        Assert.Equal(cast.Room.Id, post.Room.Id);
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();
        cut.WaitForAssertion(() => Assert.Single(snackbar.ShownSnackbars), TimeSpan.FromSeconds(5));
        Snackbar shown = Assert.Single(snackbar.ShownSnackbars);
        Assert.Equal("Asked Nova in Platform.", shown.Message);
        Assert.Equal("Asked Nova in Platform.", HumanGateCopy.AskedSnackbar("Nova", "Platform"));
        Assert.Equal(Severity.Success, shown.Severity);
    }

    /// <summary>An Ask that cannot reach the owner posts nothing and shows "Couldn't reach Ghost." (row 13).</summary>
    [Fact]
    public async Task Ask_OwnerUnknown_ShowsTheFailureSnackbarAndPostsNothing()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "Emre" }, ct);
        Cast cast = await StartWakingAsync(harness, ct, start: false);
        TaskItem task = harness.Seed(TestTasks.Make(id: "PLAT-0001", assignee: "Kai", originRoomId: cast.Room.Id));
        await using MudBunitContext ctx = NewContext(harness);
        ProofItem ghostOwned = new("citation check", false, "Ghost", null, ProofItemSource.Process);
        IRenderedComponent<ContainerFragment> cut = Render(ctx, task, new Proof([ghostOwned]), "Emre");

        cut.Find("button.task-proof-ask").Click();

        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();
        cut.WaitForAssertion(() => Assert.Single(snackbar.ShownSnackbars), TimeSpan.FromSeconds(5));
        Snackbar shown = Assert.Single(snackbar.ShownSnackbars);
        Assert.Equal("Couldn't reach Ghost.", shown.Message);
        Assert.Equal("Couldn't reach Ghost.", HumanGateCopy.AskFailedSnackbar("Ghost"));
        Assert.Equal(Severity.Warning, shown.Severity);
        Assert.Empty(harness.Posts);
    }

    /// <summary>Builds a bUnit context with the harness's real <see cref="TaskTriggerService"/> registered.</summary>
    /// <param name="harness">The composed trigger service harness.</param>
    private static MudBunitContext NewContext(Harness harness)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(harness.Trigger);
        return ctx;
    }

    /// <summary>Renders <c>TaskProofList</c> with the given parameters.</summary>
    /// <param name="ctx">The bUnit context.</param>
    /// <param name="task">The Task whose Proof is listed.</param>
    /// <param name="proof">The Proof to list.</param>
    /// <param name="human">The Human's display name.</param>
    private static IRenderedComponent<ContainerFragment> Render(MudBunitContext ctx, TaskItem task, Proof proof, string human)
    {
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskProofListComponent>(0);
            builder.AddAttribute(1, "Task", task);
            builder.AddAttribute(2, "Proof", proof);
            builder.AddAttribute(3, "HumanName", human);
            builder.CloseComponent();
        });
    }
}
