using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>Tests that <see cref="TaskDiff"/> sees and words a change to <c>human_gate</c> and <c>action</c> (Human gate Spec §6.1, Change log summaries).</summary>
public sealed class TaskDiffHumanGateTests
{
    /// <summary>Turning the gate on is one change, worded "human gate on".</summary>
    [Fact]
    public void Summarise_HumanGateOn_IsHumanGateOn()
    {
        IReadOnlyList<FieldChange> changes = TaskDiff.Compare(TestTasks.Make(), TestTasks.Make() with { HumanGate = true });

        FieldChange change = Assert.Single(changes);
        Assert.Equal(TaskField.HumanGate, change.Field);
        Assert.Equal("human gate on", TaskDiff.Summarise(changes));
    }

    /// <summary>Turning the gate off is one change, worded "human gate off".</summary>
    [Fact]
    public void Summarise_HumanGateOff_IsHumanGateOff()
    {
        IReadOnlyList<FieldChange> changes = TaskDiff.Compare(TestTasks.Make() with { HumanGate = true }, TestTasks.Make());

        Assert.Equal("human gate off", TaskDiff.Summarise(changes));
    }

    /// <summary>Setting an action is one change, worded "action set: " plus the text.</summary>
    [Fact]
    public void Summarise_ActionSet_IsActionSetWithText()
    {
        IReadOnlyList<FieldChange> changes = TaskDiff.Compare(TestTasks.Make(), TestTasks.Make() with { Action = "Send reply to Acme" });

        FieldChange change = Assert.Single(changes);
        Assert.Equal(TaskField.Action, change.Field);
        Assert.Equal("action set: Send reply to Acme", TaskDiff.Summarise(changes));
    }

    /// <summary>Changing an action reads as a set; clearing it reads "action cleared".</summary>
    [Fact]
    public void Summarise_ActionCleared_IsActionCleared()
    {
        TaskItem withAction = TestTasks.Make() with { Action = "Send reply to Acme" };

        Assert.Equal("action cleared", TaskDiff.Summarise(TaskDiff.Compare(withAction, TestTasks.Make())));
        Assert.Equal("action set: Other", TaskDiff.Summarise(TaskDiff.Compare(withAction, withAction with { Action = "Other" })));
    }

    /// <summary>The same gate and action on both sides is no change.</summary>
    [Fact]
    public void Compare_SameGateAndAction_IsUnchanged()
    {
        TaskItem task = TestTasks.Make() with { HumanGate = true, Action = "Do it" };

        Assert.Empty(TaskDiff.Compare(task, task with { }));
    }
}
