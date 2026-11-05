using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Tasks;
using MudBlazor;
using HumanGateCopy = Agency.Huddle.App.Components.Tasks.HumanGateCopy;
using ReasonDialog = Agency.Huddle.App.Components.Tasks.ReasonDialog;
using TaskDecisionCardComponent = Agency.Huddle.App.Components.Tasks.TaskDecisionCard;

namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>
/// Pins T10.1 (Spec §6.6, §6.8, §7; UI copy corrections rows 14-23, 25, 26; corrections P10):
/// the decision card, <c>TaskDecisionCard</c>, and <c>ReasonDialog</c> with <c>Required</c>.
/// The card computes the acceptance itself, calls <c>TaskService.Decide</c> with its own Task's
/// version, and reports a conflict through its own message and <c>OnReload</c>.
/// </summary>
public sealed class TaskDecisionCardTests
{
    private const string Nova = "Nova";
    private const string Human = "Emre";
    private const string SomeAction = "send the press release";
    private const string EnteredNeedsYou = "status: Review → Needs You";

    /// <summary>An <see cref="IProofSource"/> that answers every Task with a complete Proof, so the service lets Accept through.</summary>
    private sealed class CompleteProofSource : IProofSource
    {
        /// <summary>Returns a Proof of one met item.</summary>
        /// <param name="task">The Task, ignored.</param>
        /// <returns>A complete Proof.</returns>
        public Proof For(TaskItem task) => new([new ProofItem("Item", true, null, null, ProofItemSource.Process)]);
    }

    /// <summary>The Proof with three of four items met; the open one is the citation check owned by Ruth.</summary>
    /// <param name="owner">The open item's owner, or <see langword="null"/>.</param>
    /// <param name="detail">The open item's detail, or <see langword="null"/>.</param>
    private static Proof ThreeOfFour(string? owner = "Ruth", string? detail = "19 of 23 in #RSH-209") => new(
    [
        new ProofItem("Draft", true, null, null, ProofItemSource.Process),
        new ProofItem("Sources", true, null, null, ProofItemSource.Process),
        new ProofItem("Tone", true, null, null, ProofItemSource.Process),
        new ProofItem("citation check", false, owner, detail, ProofItemSource.Process),
    ]);

    /// <summary>A Proof with every item met.</summary>
    private static Proof AllMet() => new(
    [
        new ProofItem("Draft", true, null, null, ProofItemSource.Process),
        new ProofItem("Sources", true, null, null, ProofItemSource.Process),
    ]);

    /// <summary>A gated Task for display-only tests, with an optional acceptance entry and Evidence section.</summary>
    /// <param name="status">The status.</param>
    /// <param name="acceptance">"none", "valid" or "stale".</param>
    /// <param name="action">The Action, or <see langword="null"/>.</param>
    /// <param name="description">The description, which carries the Evidence section.</param>
    private static TaskItem Display(TaskState status = TaskState.NeedsYou, string acceptance = "none", string? action = null, string description = "")
    {
        TaskItem bare = TestTasks.Make(id: "PLAT-0001", status: status, assignee: Nova, description: description) with { HumanGate = true, Action = action };
        List<ChangeLogEntry> log = [TestTasks.Entry("2026-10-19T09:00:00Z", Nova, EnteredNeedsYou)];
        if (!string.Equals(acceptance, "none", StringComparison.Ordinal))
        {
            string hash = string.Equals(acceptance, "valid", StringComparison.Ordinal)
                ? ContentFingerprint.Of(bare.Title, bare.Action, bare.Description)
                : "000000000000";
            log.Add(TestTasks.Entry("2026-10-19T10:00:00Z", Human, "accepted " + hash));
        }

        return bare with { ChangeLog = log };
    }

    /// <summary>Renders the card beside the popover and dialog providers over <paramref name="harness"/>'s services.</summary>
    private static IRenderedComponent<ContainerFragment> Render(
        MudBunitContext ctx,
        TaskToolHarness harness,
        TaskItem task,
        Proof proof,
        bool blocked = false,
        List<TaskDecision>? decided = null,
        Action? onReload = null)
    {
        harness.AddTo(ctx.Services);
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskDecisionCardComponent>(0);
            builder.AddAttribute(1, "Task", task);
            builder.AddAttribute(2, "Proof", proof);
            builder.AddAttribute(3, "Blocked", blocked);
            builder.AddAttribute(4, "OnDecided", EventCallback.Factory.Create<TaskDecision>(ctx, d => decided?.Add(d)));
            builder.AddAttribute(5, "OnReload", EventCallback.Factory.Create(ctx, () => onReload?.Invoke()));
            builder.CloseComponent();
        });
    }

    /// <summary>The Human's actor.</summary>
    private static TaskActor HumanOf(TaskToolHarness harness) => TaskActors.Human(harness.Options.Value);

    /// <summary>Creates a gated Task assigned to Nova in Review, and optionally moves it into Needs You.</summary>
    private static TaskItem Gated(TaskToolHarness harness, bool needsYou, string? action = null)
    {
        TaskDraft draft = new("Write the reply", "Platform", null, TaskState.Review, Assignee: Nova) { HumanGate = true, Action = action };
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(draft, HumanOf(harness))).Task;
        if (!needsYou)
        {
            return task;
        }

        TaskActor nova = new(TaskActorKind.Agent, Nova, "nova-id");
        return Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.NeedsYou }, null, nova)).Task;
    }

    /// <summary>A button of the card (not of an open dialog) with exactly <paramref name="text"/>.</summary>
    private static IElement CardButton(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.Find(".task-decision-card").QuerySelectorAll("button").First(b => string.Equals(b.TextContent.Trim(), text, StringComparison.Ordinal));

    /// <summary>A button inside the open dialog with exactly <paramref name="text"/>.</summary>
    private static IElement DialogButton(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.Find(".mud-dialog").QuerySelectorAll("button").First(b => string.Equals(b.TextContent.Trim(), text, StringComparison.Ordinal));

    /// <summary>The card header chip reads "Your decision".</summary>
    [Fact]
    public async Task Header_ShowsTheYourDecisionChip()
    {
        using TaskToolHarness harness = new(proof: new CompleteProofSource());
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, harness, Display(), AllMet());

        Assert.Equal("Your decision", HumanGateCopy.DecisionChip);
        Assert.Equal("Your decision", cut.Find(".task-decision-card .mud-chip").TextContent.Trim());
    }

    /// <summary>With three of four items met the first line says so, verbatim.</summary>
    [Fact]
    public async Task FirstLine_IncompleteProof_CountsTheItemsThatAreIn()
    {
        using TaskToolHarness harness = new(proof: new CompleteProofSource());
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, harness, Display(), ThreeOfFour());

        Assert.Equal("3 of 4 Proof items are in.", HumanGateCopy.ProofProgress(3, 4));
        Assert.Equal("3 of 4 Proof items are in.", cut.Find(".task-decision-first-line").TextContent.Trim());
    }

    /// <summary>With every item met the first line is "All Proof is in.".</summary>
    [Fact]
    public async Task FirstLine_CompleteProof_SaysAllProofIsIn()
    {
        using TaskToolHarness harness = new(proof: new CompleteProofSource());
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, harness, Display(), AllMet());

        Assert.Equal("All Proof is in.", HumanGateCopy.AllProofIn);
        Assert.Equal("All Proof is in.", cut.Find(".task-decision-first-line").TextContent.Trim());
    }

    /// <summary>The Accept sentence names what Accept does: with no Action it marks the Task Done; with an Action it lets the assignee carry it out.</summary>
    /// <param name="action">The Action, or <see langword="null"/>.</param>
    /// <param name="expected">The sentence.</param>
    [Theory]
    [InlineData(null, "Accepting marks it Done; nothing is sent or published.")]
    [InlineData(SomeAction, "Accepting lets Nova send the press release.")]
    [InlineData("Nova: run the tests", "Accepting lets Nova run the tests.")]
    public async Task AcceptSentence_NamesWhatAcceptDoes(string? action, string expected)
    {
        using TaskToolHarness harness = new(proof: new CompleteProofSource());
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, harness, Display(action: action), AllMet());

        Assert.Equal(expected, HumanGateCopy.AcceptSentence(Nova, action));
        Assert.Equal(expected, cut.Find(".task-decision-accept-sentence").TextContent.Trim());
    }

    /// <summary>The locked notice names the first open item with its owner and detail, verbatim (Spec §6.8).</summary>
    [Fact]
    public async Task LockedNotice_NamesTheOpenItemOwnerAndDetail()
    {
        using TaskToolHarness harness = new(proof: new CompleteProofSource());
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, harness, Display(), ThreeOfFour());

        const string expected = "Proof 3/4 — citation check still open (Ruth, 19 of 23 in #RSH-209). Accept unlocks when it's in.";
        Assert.Equal(expected, HumanGateCopy.LockedNotice(3, 4, "citation check", "Ruth", "19 of 23 in #RSH-209"));
        Assert.Equal(expected, cut.Find(".task-decision-locked").TextContent.Trim());
    }

    /// <summary>A missing owner or detail drops its comma, and with both missing the parentheses go too.</summary>
    /// <param name="owner">The open item's owner.</param>
    /// <param name="detail">The open item's detail.</param>
    /// <param name="expected">The notice.</param>
    [Theory]
    [InlineData("Ruth", null, "Proof 3/4 — citation check still open (Ruth). Accept unlocks when it's in.")]
    [InlineData(null, "19 of 23 in #RSH-209", "Proof 3/4 — citation check still open (19 of 23 in #RSH-209). Accept unlocks when it's in.")]
    [InlineData(null, null, "Proof 3/4 — citation check still open. Accept unlocks when it's in.")]
    public async Task LockedNotice_MissingOwnerOrDetail_DropsTheCommaAndParentheses(string? owner, string? detail, string expected)
    {
        using TaskToolHarness harness = new(proof: new CompleteProofSource());
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, harness, Display(), ThreeOfFour(owner, detail));

        Assert.Equal(expected, HumanGateCopy.LockedNotice(3, 4, "citation check", owner, detail));
        Assert.Equal(expected, cut.Find(".task-decision-locked").TextContent.Trim());
    }

    /// <summary>The disabled Accept button is tied to the locked notice with aria-describedby.</summary>
    [Fact]
    public async Task Accept_Locked_IsDescribedByTheNotice()
    {
        using TaskToolHarness harness = new(proof: new CompleteProofSource());
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, harness, Display(), ThreeOfFour());

        IElement notice = cut.Find(".task-decision-locked");
        IElement accept = CardButton(cut, "Accept");
        Assert.False(string.IsNullOrEmpty(notice.Id));
        Assert.Equal(notice.Id, accept.GetAttribute("aria-describedby"));
    }

    /// <summary>A Task still in Review says Accept unlocks when its assignee moves it to Needs you.</summary>
    [Fact]
    public async Task ReviewLine_StillInReview_NamesTheAssignee()
    {
        using TaskToolHarness harness = new(proof: new CompleteProofSource());
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, harness, Display(TaskState.Review), AllMet());

        const string expected = "Accept unlocks when the Task moves to Needs you. It moves by itself once its Proof is in; Nova can also move it.";
        Assert.Equal(expected, HumanGateCopy.StillInReview(Nova));
        Assert.Equal(expected, cut.Find(".task-decision-review").TextContent.Trim());
    }

    /// <summary>The buttons read Request changes and Accept; Decline is not among them.</summary>
    [Fact]
    public async Task Buttons_AreRequestChangesAndAccept_DeclineIsInTheMenu()
    {
        using TaskToolHarness harness = new(proof: new CompleteProofSource());
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, harness, Display(), AllMet());

        Assert.Equal("Request changes", HumanGateCopy.RequestChangesButton);
        Assert.Equal("Accept", HumanGateCopy.AcceptButton);
        Assert.Equal("Decline", HumanGateCopy.DeclineButton);
        Assert.Equal("More decisions", HumanGateCopy.MoreDecisions);
        Assert.NotNull(CardButton(cut, "Request changes"));
        Assert.NotNull(CardButton(cut, "Accept"));
        Assert.DoesNotContain(cut.Find(".task-decision-card").QuerySelectorAll("button"), b => string.Equals(b.TextContent.Trim(), "Decline", StringComparison.Ordinal));
        Assert.Single(cut.FindAll("button[aria-label='More decisions']"));
    }

    /// <summary>Accept is enabled only in Needs You with complete Proof, no valid acceptance, and nothing unsaved.</summary>
    /// <param name="inNeedsYou">Whether the Task is in Needs You (otherwise Review).</param>
    /// <param name="complete">Whether the Proof is complete.</param>
    /// <param name="acceptance">"none", "valid" or "stale".</param>
    /// <param name="blocked">Whether there are unsaved edits.</param>
    /// <param name="enabled">Whether Accept is expected to be enabled.</param>
    [Theory]
    [InlineData(true, true, "none", false, true)]
    [InlineData(true, true, "stale", false, true)]
    [InlineData(false, true, "none", false, false)]
    [InlineData(true, false, "none", false, false)]
    [InlineData(true, true, "valid", false, false)]
    [InlineData(true, true, "none", true, false)]
    public async Task Accept_IsEnabledOnlyWhenTheSpecAllowsIt(bool inNeedsYou, bool complete, string acceptance, bool blocked, bool enabled)
    {
        using TaskToolHarness harness = new(proof: new CompleteProofSource());
        await using MudBunitContext ctx = new();
        TaskItem task = Display(inNeedsYou ? TaskState.NeedsYou : TaskState.Review, acceptance, action: string.Equals(acceptance, "valid", StringComparison.Ordinal) ? SomeAction : null);
        IRenderedComponent<ContainerFragment> cut = Render(ctx, harness, task, complete ? AllMet() : ThreeOfFour(), blocked);

        Assert.Equal(!enabled, CardButton(cut, "Accept").HasAttribute("disabled"));
    }

    /// <summary>A valid acceptance of a Task with an Action says who accepted and what the assignee can now do.</summary>
    [Fact]
    public async Task ValidAcceptance_WithAction_SaysWhoAcceptedAndWhatIsNext()
    {
        using TaskToolHarness harness = new(proof: new CompleteProofSource());
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, harness, Display(acceptance: "valid", action: SomeAction), AllMet());

        const string expected = "Accepted by Emre. Nova can now send the press release.";
        Assert.Equal(expected, HumanGateCopy.AcceptedWithAction(Human, Nova, SomeAction));
        Assert.Equal(expected, cut.Find(".task-decision-accepted").TextContent.Trim());
    }

    /// <summary>A stale acceptance shows "Changed since you accepted it." and no accepted line.</summary>
    [Fact]
    public async Task StaleAcceptance_ShowsTheStaleNotice()
    {
        using TaskToolHarness harness = new(proof: new CompleteProofSource());
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, harness, Display(acceptance: "stale", action: SomeAction), AllMet());

        Assert.Equal("Changed since you accepted it.", HumanGateCopy.StaleNotice);
        Assert.Equal("Changed since you accepted it.", cut.Find(".task-decision-stale").TextContent.Trim());
        Assert.Empty(cut.FindAll(".task-decision-accepted"));
    }

    /// <summary>The trail names the assignee, the Evidence authors other than the assignee, and the Task id.</summary>
    [Fact]
    public async Task Trail_NamesDraftedCheckedAndTracked()
    {
        using TaskToolHarness harness = new(proof: new CompleteProofSource());
        await using MudBunitContext ctx = new();
        const string description = "Body\n\n## Evidence\n2026-10-19T09:00Z | Nova | dotnet build | exit 0\n2026-10-19T09:05Z | Ruth | dotnet test | exit 0\n";
        IRenderedComponent<ContainerFragment> cut = Render(ctx, harness, Display(description: description), AllMet());

        const string expected = "Drafted by Nova · Checked by Ruth · Tracked as PLAT-0001";
        Assert.Equal(expected, HumanGateCopy.Trail(Nova, ["Ruth"], "PLAT-0001"));
        Assert.Equal(expected, cut.Find(".task-decision-trail").TextContent.Trim());
    }

    /// <summary>With no Evidence from anyone else the trail has no Checked by part.</summary>
    [Fact]
    public async Task Trail_NoOtherEvidenceAuthors_OmitsCheckedBy()
    {
        using TaskToolHarness harness = new(proof: new CompleteProofSource());
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = Render(ctx, harness, Display(), AllMet());

        const string expected = "Drafted by Nova · Tracked as PLAT-0001";
        Assert.Equal(expected, HumanGateCopy.Trail(Nova, [], "PLAT-0001"));
        Assert.Equal(expected, cut.Find(".task-decision-trail").TextContent.Trim());
    }

    /// <summary>Accept calls Decide with the Task's own version; a Task with no Action lands in Done and OnDecided reports Accept.</summary>
    [Fact]
    public async Task Accept_Clicked_DecidesAndReportsAccept()
    {
        using TaskToolHarness harness = new(proof: new CompleteProofSource());
        await using MudBunitContext ctx = new();
        TaskItem task = Gated(harness, needsYou: true);
        List<TaskDecision> decided = [];
        IRenderedComponent<ContainerFragment> cut = Render(ctx, harness, task, AllMet(), decided: decided);

        await cut.InvokeAsync(() => CardButton(cut, "Accept").ClickAsync(new MouseEventArgs()));

        cut.WaitForAssertion(() => Assert.Equal([TaskDecision.Accept], decided), TimeSpan.FromSeconds(5));
        Assert.Equal(TaskState.Done, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>Request changes opens ReasonDialog titled for the Task, with a required Reason field, no Skip, and Confirm disabled while blank.</summary>
    [Fact]
    public async Task RequestChanges_OpensTheRequiredReasonDialog()
    {
        using TaskToolHarness harness = new(proof: new CompleteProofSource());
        await using MudBunitContext ctx = new();
        TaskItem task = Gated(harness, needsYou: true);
        IRenderedComponent<ContainerFragment> cut = Render(ctx, harness, task, AllMet());

        await cut.InvokeAsync(() => CardButton(cut, "Request changes").ClickAsync(new MouseEventArgs()));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<ReasonDialog>()));

        Assert.Equal($"Request changes on {task.Id}", HumanGateCopy.RequestChangesTitle(task.Id.ToString()));
        Assert.Equal($"Request changes on {task.Id}", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal("Reason", HumanGateCopy.ReasonLabel);
        Assert.Equal("Reason", cut.FindComponent<MudTextField<string>>().Instance.Label);
        Assert.DoesNotContain(cut.Find(".mud-dialog").QuerySelectorAll("button"), b => string.Equals(b.TextContent.Trim(), "Skip", StringComparison.Ordinal));
        Assert.True(DialogButton(cut, "Request changes").HasAttribute("disabled"));
    }

    /// <summary>The note typed in the dialog is sent: the Task goes back to In Progress with "changes requested: note" and OnDecided reports RequestChanges.</summary>
    [Fact]
    public async Task RequestChanges_WithANote_CallsDecideWithTheNote()
    {
        using TaskToolHarness harness = new(proof: new CompleteProofSource());
        await using MudBunitContext ctx = new();
        TaskItem task = Gated(harness, needsYou: true);
        List<TaskDecision> decided = [];
        IRenderedComponent<ContainerFragment> cut = Render(ctx, harness, task, AllMet(), decided: decided);

        await cut.InvokeAsync(() => CardButton(cut, "Request changes").ClickAsync(new MouseEventArgs()));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<ReasonDialog>()));
        MudTextField<string> field = cut.FindComponent<MudTextField<string>>().Instance;
        await cut.InvokeAsync(() => field.ValueChanged.InvokeAsync("Cite the second source"));
        cut.WaitForAssertion(() => Assert.False(DialogButton(cut, "Request changes").HasAttribute("disabled")));
        await cut.InvokeAsync(() => DialogButton(cut, "Request changes").ClickAsync(new MouseEventArgs()));

        cut.WaitForAssertion(() => Assert.Equal([TaskDecision.RequestChanges], decided), TimeSpan.FromSeconds(5));
        TaskItem? saved = harness.Store.Get(task.Id);
        Assert.Equal(TaskState.InProgress, saved?.Status);
        Assert.Contains(saved?.ChangeLog ?? [], e => string.Equals(e.Summary, "changes requested: Cite the second source", StringComparison.Ordinal)); // contains-ok: a Change log entry found by exact summary among the Task's entries.
    }

    /// <summary>Decline sits in the overflow menu, opens the required Reason dialog titled Decline, and sends the note: the Task is Rejected.</summary>
    [Fact]
    public async Task Decline_InTheOverflowMenu_OpensTheDialogAndDeclinesWithTheNote()
    {
        using TaskToolHarness harness = new(proof: new CompleteProofSource());
        await using MudBunitContext ctx = new();
        TaskItem task = Gated(harness, needsYou: true);
        List<TaskDecision> decided = [];
        IRenderedComponent<ContainerFragment> cut = Render(ctx, harness, task, AllMet(), decided: decided);

        await cut.InvokeAsync(() => cut.Find("button[aria-label='More decisions']").ClickAsync(new MouseEventArgs()));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("div.mud-menu-item"), e => string.Equals(e.TextContent.Trim(), "Decline", StringComparison.Ordinal)));
        await cut.InvokeAsync(() => cut.FindAll("div.mud-menu-item").First(e => string.Equals(e.TextContent.Trim(), "Decline", StringComparison.Ordinal)).ClickAsync(new MouseEventArgs()));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<ReasonDialog>()));

        Assert.Equal($"Decline {task.Id}", HumanGateCopy.DeclineTitle(task.Id.ToString()));
        Assert.Equal($"Decline {task.Id}", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.True(DialogButton(cut, "Decline").HasAttribute("disabled"));
        MudTextField<string> field = cut.FindComponent<MudTextField<string>>().Instance;
        await cut.InvokeAsync(() => field.ValueChanged.InvokeAsync("Out of scope"));
        cut.WaitForAssertion(() => Assert.False(DialogButton(cut, "Decline").HasAttribute("disabled")));
        await cut.InvokeAsync(() => DialogButton(cut, "Decline").ClickAsync(new MouseEventArgs()));

        cut.WaitForAssertion(() => Assert.Equal([TaskDecision.Decline], decided), TimeSpan.FromSeconds(5));
        Assert.Equal(TaskState.Rejected, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>A Task that changed under the card is a version conflict: the card shows its message and raises OnReload, and nothing is decided.</summary>
    [Fact]
    public async Task Accept_OnAStaleVersion_ShowsTheConflictMessageAndRaisesOnReload()
    {
        using TaskToolHarness harness = new(proof: new CompleteProofSource());
        await using MudBunitContext ctx = new();
        TaskItem task = Gated(harness, needsYou: true);
        List<TaskDecision> decided = [];
        int reloads = 0;
        IRenderedComponent<ContainerFragment> cut = Render(ctx, harness, task, AllMet(), decided: decided, onReload: () => reloads++);
        _ = harness.Service.Update(task.Id, new TaskPatch { Title = "Renamed behind the card" }, null, HumanOf(harness));

        await cut.InvokeAsync(() => CardButton(cut, "Accept").ClickAsync(new MouseEventArgs()));

        cut.WaitForAssertion(() => Assert.Equal(1, reloads), TimeSpan.FromSeconds(5));
        Assert.Equal("This Task changed while you were reading it.", HumanGateCopy.VersionConflict);
        Assert.Equal("This Task changed while you were reading it.", cut.Find(".task-decision-message").TextContent.Trim());
        Assert.Empty(decided);
        Assert.Equal(TaskState.NeedsYou, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>Required ReasonDialog: no Skip button, the field is labelled Reason, and the confirm button is disabled while the field is blank.</summary>
    [Fact]
    public async Task ReasonDialog_Required_HasNoSkipLabelsReasonAndDisablesConfirmWhileBlank()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        IDialogService dialogs = ctx.Services.GetRequiredService<IDialogService>();
        DialogParameters parameters = new() { ["Required"] = true, ["ConfirmText"] = "Decline" };

        _ = await cut.InvokeAsync(() => dialogs.ShowAsync<ReasonDialog>("Decline PLAT-0001", parameters));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<ReasonDialog>()));

        Assert.Equal("Reason", cut.FindComponent<MudTextField<string>>().Instance.Label);
        Assert.DoesNotContain(cut.Find(".mud-dialog").QuerySelectorAll("button"), b => string.Equals(b.TextContent.Trim(), "Skip", StringComparison.Ordinal));
        Assert.True(DialogButton(cut, "Decline").HasAttribute("disabled"));
        MudTextField<string> field = cut.FindComponent<MudTextField<string>>().Instance;
        await cut.InvokeAsync(() => field.ValueChanged.InvokeAsync("   "));
        Assert.True(DialogButton(cut, "Decline").HasAttribute("disabled"));
        await cut.InvokeAsync(() => field.ValueChanged.InvokeAsync("Because"));
        cut.WaitForAssertion(() => Assert.False(DialogButton(cut, "Decline").HasAttribute("disabled")));
    }
}
