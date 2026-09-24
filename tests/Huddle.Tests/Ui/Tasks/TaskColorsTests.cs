using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using MudBlazor;

namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>
/// Pins the <see cref="TaskColors"/> mapping from Spec §13.10: every <see cref="TaskState"/>,
/// <see cref="TaskPriority"/> and <see cref="PresenceState"/> maps to an exact <see cref="Color"/>,
/// and every priority also carries a non-empty icon.
/// </summary>
public sealed class TaskColorsTests
{
    /// <summary>Every <see cref="TaskState"/> maps to the <see cref="Color"/> Spec §13.10 names.</summary>
    [Theory]
    [InlineData(TaskState.Backlog, Color.Default)]
    [InlineData(TaskState.ToDo, Color.Info)]
    [InlineData(TaskState.InProgress, Color.Warning)]
    [InlineData(TaskState.Review, Color.Secondary)]
    [InlineData(TaskState.Done, Color.Success)]
    [InlineData(TaskState.Cancelled, Color.Error)]
    [InlineData(TaskState.Duplicate, Color.Error)]
    [InlineData(TaskState.Rejected, Color.Error)]
    public void For_EveryTaskState_ReturnsSpecColor(TaskState state, Color expected)
    {
        Color actual = TaskColors.For(state);

        Assert.Equal(expected, actual);
    }

    /// <summary>Every <see cref="TaskPriority"/> maps to the <see cref="Color"/> Spec §13.10 names.</summary>
    [Theory]
    [InlineData(TaskPriority.Low, Color.Default)]
    [InlineData(TaskPriority.Medium, Color.Info)]
    [InlineData(TaskPriority.High, Color.Warning)]
    [InlineData(TaskPriority.Urgent, Color.Error)]
    public void For_EveryTaskPriority_ReturnsSpecColor(TaskPriority priority, Color expected)
    {
        Color actual = TaskColors.For(priority);

        Assert.Equal(expected, actual);
    }

    /// <summary>Every <see cref="PresenceState"/> maps to the <see cref="Color"/> Spec §13.10 names.</summary>
    [Theory]
    [InlineData(PresenceState.Awake, Color.Success)]
    [InlineData(PresenceState.Asleep, Color.Default)]
    [InlineData(PresenceState.Offline, Color.Error)]
    public void For_EveryPresenceState_ReturnsSpecColor(PresenceState presence, Color expected)
    {
        Color actual = TaskColors.For(presence);

        Assert.Equal(expected, actual);
    }

    /// <summary>A priority is always shown with an icon, so every value must carry a non-empty one.</summary>
    [Theory]
    [InlineData(TaskPriority.Low)]
    [InlineData(TaskPriority.Medium)]
    [InlineData(TaskPriority.High)]
    [InlineData(TaskPriority.Urgent)]
    public void Icon_EveryPriority_NonEmpty(TaskPriority priority)
    {
        string icon = TaskColors.Icon(priority);

        Assert.False(string.IsNullOrEmpty(icon));
    }

    /// <summary>The icon for each priority is the exact glyph Task 11.1.i names, checked against the constant itself.</summary>
    [Fact]
    public void Icon_EveryPriority_MatchesNamedGlyph()
    {
        Assert.Equal(Icons.Material.Filled.ArrowDownward, TaskColors.Icon(TaskPriority.Low));
        Assert.Equal(Icons.Material.Filled.Remove, TaskColors.Icon(TaskPriority.Medium));
        Assert.Equal(Icons.Material.Filled.ArrowUpward, TaskColors.Icon(TaskPriority.High));
        Assert.Equal(Icons.Material.Filled.PriorityHigh, TaskColors.Icon(TaskPriority.Urgent));
    }
}
