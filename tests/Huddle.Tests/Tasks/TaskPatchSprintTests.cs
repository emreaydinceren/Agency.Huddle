using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>Tests for the Sprint field of <see cref="TaskPatch"/> and <see cref="TaskDraft"/> (Sprints spec §9.3).</summary>
public sealed class TaskPatchSprintTests
{
    /// <summary>A patch that sets only a Sprint is not empty and reports exactly the Sprint field.</summary>
    [Fact]
    public void Patch_SprintSet_IsNotEmpty_AndReportsSprintField()
    {
        TaskPatch patch = new() { Sprint = Optional<SprintId?>.Set(new SprintId(2)) };

        Assert.False(patch.IsEmpty);
        Assert.Equal([TaskField.Sprint], patch.Fields());
    }

    /// <summary>A patch with no Sprint set leaves it out of <c>Fields()</c> and stays empty.</summary>
    [Fact]
    public void Patch_SprintNotSet_IsEmpty()
    {
        TaskPatch patch = new();

        Assert.True(patch.IsEmpty);
        Assert.DoesNotContain(TaskField.Sprint, patch.Fields());
    }

    /// <summary>Applying a set Sprint replaces it, <c>Set(null)</c> clears it and an unset patch leaves it alone.</summary>
    [Fact]
    public void Patch_ApplyTo_SetsClearsOrKeepsTheSprint()
    {
        TaskItem task = TestTasks.Make(sprint: new SprintId(1));

        TaskItem moved = new TaskPatch { Sprint = Optional<SprintId?>.Set(new SprintId(2)) }.ApplyTo(task);
        TaskItem cleared = new TaskPatch { Sprint = Optional<SprintId?>.Set(null) }.ApplyTo(task);
        TaskItem kept = new TaskPatch { Title = "Other" }.ApplyTo(task);

        Assert.Equal(new SprintId(2), moved.Sprint);
        Assert.Null(cleared.Sprint);
        Assert.Equal(new SprintId(1), kept.Sprint);
    }

    /// <summary>A draft's Sprint is its last parameter and defaults to none, so positional callers still compile.</summary>
    [Fact]
    public void Draft_SprintIsLastAndDefaultsToNull()
    {
        TaskDraft plain = new("Title", "Platform", null);
        TaskDraft withSprint = new("Title", "Platform", null, Sprint: new SprintId(3));

        Assert.Null(plain.Sprint);
        Assert.Equal(new SprintId(3), withSprint.Sprint);
        Assert.Equal("Sprint", typeof(TaskDraft).GetConstructors()[0].GetParameters()[^1].Name);
    }
}
