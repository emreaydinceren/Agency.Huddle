using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using ContentFingerprint = Agency.Huddle.App.Tasks.HumanGate.ContentFingerprint;
using HumanGateRules = Agency.Huddle.App.Tasks.HumanGate.HumanGateRules;
using Proof = Agency.Huddle.App.Tasks.HumanGate.Proof;
using ProofItem = Agency.Huddle.App.Tasks.HumanGate.ProofItem;

namespace Agency.Huddle.Tests.Tasks.HumanGate;

/// <summary>Tests for <c>HumanGateRules.Check</c>, rules H-1 to H-5 (Human gate Spec §6.4, corrections-P4).</summary>
public sealed class HumanGateRulesTests
{
    private const string Human = "Emre";
    private const string Entered = "status: Review → Needs You (Proof 4/4)";

    private static readonly TaskActor TheHuman = new(TaskActorKind.Human, Human, "u-human");
    private static readonly TaskActor AnAgent = new(TaskActorKind.Agent, "Ada", "u-ada");

    /// <summary>A Proof with four items, <paramref name="met"/> of them met; the first open one is "Citation check".</summary>
    /// <param name="met">How many of the four items are met, counting from the end.</param>
    private static Proof ProofWith(int met)
    {
        string[] labels = ["Summary written", "Sources listed", "Tests pass", "Citation check"];
        List<ProofItem> items = [];
        for (int index = 0; index < labels.Length; index++)
        {
            items.Add(new ProofItem(labels[index], index < met, null, null, ProofItemSource.Process));
        }

        return new Proof(items);
    }

    /// <summary>A Task carrying the Human gate in the given status.</summary>
    /// <param name="status">The status.</param>
    private static TaskItem Gated(TaskState status) =>
        TestTasks.Make(id: "PLAT-0001", title: "Write the summary", status: status, description: "Body") with { HumanGate = true };

    /// <summary>A gated Needs You Task with an acceptance entry at its current content, plus the entry into Needs You.</summary>
    private static TaskItem Accepted()
    {
        TaskItem task = Gated(TaskState.NeedsYou);
        string hash = ContentFingerprint.Of(task.Title, task.Action, task.Description);
        return task with
        {
            ChangeLog = [TestTasks.Entry("2026-10-01T09:00:00Z", "Ada", Entered), TestTasks.Entry("2026-10-02T09:00:00Z", Human, "accepted " + hash)],
        };
    }

    /// <summary>Asserts a refusal with the given gate and exact message.</summary>
    /// <param name="refusal">What the check returned.</param>
    /// <param name="gate">The expected gate.</param>
    /// <param name="message">The expected message.</param>
    private static void AssertRefused(GateRefusal? refusal, GateId gate, string message)
    {
        Assert.NotNull(refusal);
        Assert.Equal(gate, refusal.Gate);
        Assert.Equal(message, refusal.Message);
    }

    /// <summary>H-1: an Agent turning the Human gate off is refused.</summary>
    [Fact]
    public void H1_AgentTurnsGateOff_IsRefused()
    {
        GateRefusal? refusal = HumanGateRules.Check(Gated(TaskState.ToDo), Gated(TaskState.ToDo) with { HumanGate = false }, AnAgent, ProofWith(0), Human);

        AssertRefused(refusal, GateId.HumanGate, "Only Emre can turn off the Human gate on PLAT-0001.");
    }

    /// <summary>H-1: the Human turning the Human gate off is allowed.</summary>
    [Fact]
    public void H1_HumanTurnsGateOff_IsAllowed()
    {
        Assert.Null(HumanGateRules.Check(Gated(TaskState.ToDo), Gated(TaskState.ToDo) with { HumanGate = false }, TheHuman, ProofWith(0), Human));
    }

    /// <summary>H-1: an outside edit by the Human's id is judged by its kind, not its user id, so it is refused like an Agent.</summary>
    [Fact]
    public void H1_OutsideEditWithHumanId_IsRefused()
    {
        TaskActor outside = new(TaskActorKind.OutsideHuddle, Human, "u-human");

        GateRefusal? refusal = HumanGateRules.Check(Gated(TaskState.ToDo), Gated(TaskState.ToDo) with { HumanGate = false }, outside, ProofWith(0), Human);

        AssertRefused(refusal, GateId.HumanGate, "Only Emre can turn off the Human gate on PLAT-0001.");
    }

    /// <summary>H-2: an Agent clearing the Action is refused.</summary>
    [Fact]
    public void H2_AgentClearsAction_IsRefused()
    {
        TaskItem before = Gated(TaskState.ToDo) with { Action = "Read it" };

        GateRefusal? refusal = HumanGateRules.Check(before, before with { Action = null }, AnAgent, ProofWith(0), Human);

        AssertRefused(refusal, GateId.HumanGate, "Only Emre can turn off the Human gate on PLAT-0001.");
    }

    /// <summary>H-2: the Human clearing the Action is allowed.</summary>
    [Fact]
    public void H2_HumanClearsAction_IsAllowed()
    {
        TaskItem before = Gated(TaskState.ToDo) with { Action = "Read it" };

        Assert.Null(HumanGateRules.Check(before, before with { Action = null }, TheHuman, ProofWith(0), Human));
    }

    /// <summary>H-3: moving a gated Task to Needs You with an open Proof item is refused for an Agent, naming the first open item.</summary>
    [Fact]
    public void H3_AgentEntersNeedsYouWithOpenProof_IsRefused()
    {
        GateRefusal? refusal = HumanGateRules.Check(Gated(TaskState.Review), Gated(TaskState.NeedsYou), AnAgent, ProofWith(3), Human);

        AssertRefused(refusal, GateId.ProofIncomplete, "PLAT-0001 can go to Needs You once its Proof is complete (3/4: citation check is open).");
    }

    /// <summary>H-3: the Human is refused the same way when the Proof is incomplete.</summary>
    [Fact]
    public void H3_HumanEntersNeedsYouWithOpenProof_IsRefused()
    {
        GateRefusal? refusal = HumanGateRules.Check(Gated(TaskState.Review), Gated(TaskState.NeedsYou), TheHuman, ProofWith(1), Human);

        AssertRefused(refusal, GateId.ProofIncomplete, "PLAT-0001 can go to Needs You once its Proof is complete (1/4: sources listed is open).");
    }

    /// <summary>H-3: with a complete Proof either actor may move a gated Task into Needs You.</summary>
    [Fact]
    public void H3_EntersNeedsYouWithCompleteProof_IsAllowed()
    {
        Assert.Null(HumanGateRules.Check(Gated(TaskState.Review), Gated(TaskState.NeedsYou), AnAgent, ProofWith(4), Human));
        Assert.Null(HumanGateRules.Check(Gated(TaskState.Review), Gated(TaskState.NeedsYou), TheHuman, ProofWith(4), Human));
    }

    /// <summary>H-3: an ungated Task cannot enter Needs You, whoever asks.</summary>
    [Fact]
    public void H3_UngatedTaskEntersNeedsYou_IsRefused()
    {
        TaskItem before = TestTasks.Make(id: "PLAT-0001", status: TaskState.Review);
        TaskItem after = before with { Status = TaskState.NeedsYou };

        AssertRefused(HumanGateRules.Check(before, after, AnAgent, ProofWith(4), Human), GateId.ProofIncomplete, "PLAT-0001 isn't Human-gated, so it goes from Review to Done.");
        AssertRefused(HumanGateRules.Check(before, after, TheHuman, ProofWith(4), Human), GateId.ProofIncomplete, "PLAT-0001 isn't Human-gated, so it goes from Review to Done.");
    }

    /// <summary>H-4: a gated Task in Needs You with no acceptance cannot go to Done, for an Agent or the Human.</summary>
    [Fact]
    public void H4_DoneWithoutAcceptance_IsRefused()
    {
        TaskItem before = Gated(TaskState.NeedsYou) with { ChangeLog = [TestTasks.Entry("2026-10-01T09:00:00Z", "Ada", Entered)] };
        TaskItem after = before with { Status = TaskState.Done };
        const string message = "PLAT-0001 needs Emre's acceptance. Move it to Needs You when the Proof is complete; you'll be woken when Emre decides.";

        AssertRefused(HumanGateRules.Check(before, after, AnAgent, ProofWith(4), Human), GateId.HumanGate, message);
        AssertRefused(HumanGateRules.Check(before, after, TheHuman, ProofWith(4), Human), GateId.HumanGate, message);
    }

    /// <summary>H-4: an acceptance that has gone Stale does not open Done.</summary>
    [Fact]
    public void H4_DoneWithStaleAcceptance_IsRefused()
    {
        TaskItem before = Accepted() with { Title = "Changed after accepting" };

        GateRefusal? refusal = HumanGateRules.Check(before, before with { Status = TaskState.Done }, AnAgent, ProofWith(4), Human);

        Assert.NotNull(refusal);
        Assert.Equal(GateId.HumanGate, refusal.Gate);
    }

    /// <summary>H-4: an edit to the content in the same change that moves to Done fails the fingerprint.</summary>
    [Fact]
    public void H4_DoneWithContentChangedInTheSameChange_IsRefused()
    {
        TaskItem before = Accepted();

        GateRefusal? refusal = HumanGateRules.Check(before, before with { Status = TaskState.Done, Description = "Sneaked in" }, AnAgent, ProofWith(4), Human);

        Assert.NotNull(refusal);
        Assert.Equal(GateId.HumanGate, refusal.Gate);
    }

    /// <summary>H-4: a valid Accept plus an edit to the description in the same change as Done is refused with text that says the edit would void the Accept.</summary>
    [Fact]
    public void H4_ValidAcceptWithDescriptionEditedInTheDoneChange_SaysTheEditVoidsTheAccept()
    {
        TaskItem before = Accepted();
        const string message = "PLAT-0001 has Emre's Accept, and this change to its title, Action or description would void it. Send the status Done on its own, and put your notes in Evidence.";

        AssertRefused(HumanGateRules.Check(before, before with { Status = TaskState.Done, Description = "Notes added" }, AnAgent, ProofWith(4), Human), GateId.HumanGate, message);
        AssertRefused(HumanGateRules.Check(before, before with { Status = TaskState.Done, Title = "Retitled" }, AnAgent, ProofWith(4), Human), GateId.HumanGate, message);
        AssertRefused(HumanGateRules.Check(before, before with { Status = TaskState.Done, Action = "Send it" }, AnAgent, ProofWith(4), Human), GateId.HumanGate, message);
    }

    /// <summary>H-4: with a valid Accept and nothing edited, Done is allowed.</summary>
    [Fact]
    public void H4_ValidAcceptAndNoEdit_IsAllowed()
    {
        TaskItem before = Accepted();

        Assert.Null(HumanGateRules.Check(before, before with { Status = TaskState.Done }, AnAgent, ProofWith(4), Human));
    }

    /// <summary>H-4: with no Accept, an edit in the Done change keeps the old text.</summary>
    [Fact]
    public void H4_NoAcceptAndDescriptionEdited_KeepsTheNeedsAcceptanceText()
    {
        TaskItem before = Gated(TaskState.NeedsYou) with { ChangeLog = [TestTasks.Entry("2026-10-01T09:00:00Z", "Ada", Entered)] };

        AssertRefused(
            HumanGateRules.Check(before, before with { Status = TaskState.Done, Description = "Notes" }, AnAgent, ProofWith(4), Human),
            GateId.HumanGate,
            "PLAT-0001 needs Emre's acceptance. Move it to Needs You when the Proof is complete; you'll be woken when Emre decides.");
    }

    /// <summary>H-4: an incomplete Proof blocks Done even with a valid acceptance.</summary>
    [Fact]
    public void H4_DoneWithIncompleteProof_IsRefused()
    {
        TaskItem before = Accepted();

        GateRefusal? refusal = HumanGateRules.Check(before, before with { Status = TaskState.Done }, TheHuman, ProofWith(3), Human);

        Assert.NotNull(refusal);
        Assert.Equal(GateId.HumanGate, refusal.Gate);
    }

    /// <summary>H-4: Done straight from Review, skipping Needs You, is refused on a gated Task.</summary>
    [Fact]
    public void H4_DoneFromReview_IsRefused()
    {
        TaskItem before = Gated(TaskState.Review);

        GateRefusal? refusal = HumanGateRules.Check(before, before with { Status = TaskState.Done }, TheHuman, ProofWith(4), Human);

        Assert.NotNull(refusal);
        Assert.Equal(GateId.HumanGate, refusal.Gate);
    }

    /// <summary>H-4: from Needs You with a valid acceptance and a complete Proof, either actor may complete the Task.</summary>
    [Fact]
    public void H4_DoneWithValidAcceptanceAndCompleteProof_IsAllowed()
    {
        TaskItem before = Accepted();
        TaskItem after = before with { Status = TaskState.Done };

        Assert.Null(HumanGateRules.Check(before, after, AnAgent, ProofWith(4), Human));
        Assert.Null(HumanGateRules.Check(before, after, TheHuman, ProofWith(4), Human));
    }

    /// <summary>H-4: logging Evidence in the same change as Done does not break the fingerprint.</summary>
    [Fact]
    public void H4_DoneWithEvidenceAppended_IsAllowed()
    {
        TaskItem before = Accepted();

        Assert.Null(HumanGateRules.Check(before, before with { Status = TaskState.Done, Description = "Body\n## Evidence\n- 2026-10-02 Ada did it" }, AnAgent, ProofWith(4), Human));
    }

    /// <summary>Moving a gated Task out of Needs You to an earlier status is allowed for anyone.</summary>
    [Fact]
    public void LeavingNeedsYouForAnEarlierStatus_IsAllowedForAnyone()
    {
        TaskItem before = Accepted();
        TaskItem after = before with { Status = TaskState.Review };

        Assert.Null(HumanGateRules.Check(before, after, AnAgent, ProofWith(0), Human));
        Assert.Null(HumanGateRules.Check(before, after, TheHuman, ProofWith(0), Human));
    }

    /// <summary>H-5: creating a gated Task in Needs You is refused for an Agent and the Human.</summary>
    [Fact]
    public void H5_CreateGatedInNeedsYou_IsRefused()
    {
        const string message = "PLAT-0001 is Human-gated, so it starts before Needs You.";

        AssertRefused(HumanGateRules.Check(null, Gated(TaskState.NeedsYou), AnAgent, ProofWith(4), Human), GateId.HumanGate, message);
        AssertRefused(HumanGateRules.Check(null, Gated(TaskState.NeedsYou), TheHuman, ProofWith(4), Human), GateId.HumanGate, message);
    }

    /// <summary>H-5: creating a gated Task in Done is refused for an Agent and the Human.</summary>
    [Fact]
    public void H5_CreateGatedInDone_IsRefused()
    {
        const string message = "PLAT-0001 is Human-gated, so it starts before Needs You.";

        AssertRefused(HumanGateRules.Check(null, Gated(TaskState.Done), AnAgent, ProofWith(4), Human), GateId.HumanGate, message);
        AssertRefused(HumanGateRules.Check(null, Gated(TaskState.Done), TheHuman, ProofWith(4), Human), GateId.HumanGate, message);
    }

    /// <summary>H-5: creating a gated Task in an earlier status is allowed.</summary>
    [Fact]
    public void H5_CreateGatedInToDo_IsAllowed()
    {
        Assert.Null(HumanGateRules.Check(null, Gated(TaskState.ToDo), AnAgent, ProofWith(0), Human));
        Assert.Null(HumanGateRules.Check(null, Gated(TaskState.ToDo), TheHuman, ProofWith(0), Human));
    }

    /// <summary>An ungated Task is refused only the move into Needs You; ordinary moves, including Done, pass.</summary>
    [Fact]
    public void Check_UngatedTask_RefusesOnlyEntryIntoNeedsYou()
    {
        TaskItem review = TestTasks.Make(id: "PLAT-0001", status: TaskState.Review);

        Assert.Null(HumanGateRules.Check(review, review with { Status = TaskState.Done }, AnAgent, ProofWith(0), Human));
        Assert.Null(HumanGateRules.Check(review with { Status = TaskState.ToDo }, review with { Status = TaskState.InProgress }, AnAgent, ProofWith(0), Human));
        Assert.Null(HumanGateRules.Check(null, review with { Status = TaskState.Done }, AnAgent, ProofWith(0), Human));
        Assert.NotNull(HumanGateRules.Check(review, review with { Status = TaskState.NeedsYou }, AnAgent, ProofWith(0), Human));
    }

    /// <summary>The two new gate ids can never be read from a Process.md and have their own ids and names.</summary>
    [Fact]
    public void TryParseGate_NeverReturnsHumanGateOrProofIncomplete()
    {
        Assert.False(ProcessDocument.TryParseGate("human-gate", out _));
        Assert.False(ProcessDocument.TryParseGate("proof-incomplete", out _));
        Assert.Equal("human-gate", ProcessDocument.ToId(GateId.HumanGate));
        Assert.Equal("proof-incomplete", ProcessDocument.ToId(GateId.ProofIncomplete));
        Assert.Equal("Human gate", ProcessDocument.DisplayName(GateId.HumanGate));
        Assert.Equal("Proof incomplete", ProcessDocument.DisplayName(GateId.ProofIncomplete));
    }
}
