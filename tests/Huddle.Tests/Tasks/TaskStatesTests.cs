using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>Tests for <see cref="TaskState"/> and <see cref="TaskStates"/>.</summary>
public sealed class TaskStatesTests
{
    /// <summary>Every state has its wire name as specified in §6.1.</summary>
    [Theory]
    [InlineData(TaskState.Backlog, "Backlog")]
    [InlineData(TaskState.ToDo, "To Do")]
    [InlineData(TaskState.InProgress, "In Progress")]
    [InlineData(TaskState.Review, "Review")]
    [InlineData(TaskState.Done, "Done")]
    [InlineData(TaskState.Cancelled, "Cancelled")]
    [InlineData(TaskState.Duplicate, "Duplicate")]
    [InlineData(TaskState.Rejected, "Rejected")]
    public void ToWire_EveryState_MatchesSpecWireName(TaskState state, string expected)
    {
        string wire = state.ToWire();
        Assert.Equal(expected, wire);
    }

    /// <summary>Every wire name parses back to its state.</summary>
    [Theory]
    [InlineData(TaskState.Backlog)]
    [InlineData(TaskState.ToDo)]
    [InlineData(TaskState.InProgress)]
    [InlineData(TaskState.Review)]
    [InlineData(TaskState.Done)]
    [InlineData(TaskState.Cancelled)]
    [InlineData(TaskState.Duplicate)]
    [InlineData(TaskState.Rejected)]
    public void TryParse_WireName_RoundTrips(TaskState state)
    {
        string wire = state.ToWire();
        bool parsed = TaskStates.TryParse(wire, out TaskState result);
        Assert.True(parsed);
        Assert.Equal(state, result);
    }

    /// <summary>Aliases are accepted: enum identifiers, snake_case, trimmed, case-insensitive.</summary>
    [Theory]
    [InlineData("todo")]
    [InlineData("ToDo")]
    [InlineData("to_do")]
    [InlineData(" To Do ")]
    [InlineData("IN_PROGRESS")]
    [InlineData("inprogress")]
    public void TryParse_Aliases_Accepted(string alias)
    {
        bool parsed = TaskStates.TryParse(alias, out _);
        Assert.True(parsed);
    }

    /// <summary>Unknown state names are rejected.</summary>
    [Theory]
    [InlineData("Doing")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_Unknown_ReturnsFalse(string? unknown)
    {
        bool parsed = TaskStates.TryParse(unknown, out _);
        Assert.False(parsed);
    }

    /// <summary>Exactly four states are terminal: Done, Cancelled, Duplicate, Rejected.</summary>
    [Fact]
    public void IsTerminal_ExactlyFourStates()
    {
        List<TaskState> terminals = [];
        foreach (TaskState state in TaskStates.All)
        {
            if (state.IsTerminal())
            {
                terminals.Add(state);
            }
        }

        Assert.Equal([TaskState.Done, TaskState.Cancelled, TaskState.Duplicate, TaskState.Rejected], terminals);
    }

    /// <summary>Exactly three states are "Won't do": Cancelled, Duplicate, Rejected.</summary>
    [Fact]
    public void IsWontDo_ExactlyThreeStates()
    {
        List<TaskState> wontDo = [];
        foreach (TaskState state in TaskStates.All)
        {
            if (state.IsWontDo())
            {
                wontDo.Add(state);
            }
        }

        Assert.Equal([TaskState.Cancelled, TaskState.Duplicate, TaskState.Rejected], wontDo);
    }

    /// <summary>All states are in declaration order.</summary>
    [Fact]
    public void All_IsDeclarationOrder()
    {
        Assert.Equal(
        [
            TaskState.Backlog,
            TaskState.ToDo,
            TaskState.InProgress,
            TaskState.Review,
            TaskState.Done,
            TaskState.Cancelled,
            TaskState.Duplicate,
            TaskState.Rejected,
        ],
            TaskStates.All);
    }
}

/// <summary>Tests for <see cref="TaskPriority"/> and <see cref="TaskPriorities"/>.</summary>
public sealed class TaskPrioritiesTests
{
    /// <summary>Every priority has its wire name and parses back.</summary>
    [Theory]
    [InlineData(TaskPriority.Low, "Low")]
    [InlineData(TaskPriority.Medium, "Medium")]
    [InlineData(TaskPriority.High, "High")]
    [InlineData(TaskPriority.Urgent, "Urgent")]
    public void ToWire_EveryPriority_MatchesSpec(TaskPriority priority, string expected)
    {
        string wire = priority.ToWire();
        Assert.Equal(expected, wire);
    }

    /// <summary>Wire names parse back to their priority, case-insensitively.</summary>
    [Theory]
    [InlineData(TaskPriority.Low)]
    [InlineData(TaskPriority.Medium)]
    [InlineData(TaskPriority.High)]
    [InlineData(TaskPriority.Urgent)]
    public void TryParse_WireName_RoundTrips(TaskPriority priority)
    {
        string wire = priority.ToWire();
        bool parsed = TaskPriorities.TryParse(wire, out TaskPriority result);
        Assert.True(parsed);
        Assert.Equal(priority, result);
    }

    /// <summary>Parsing is case-insensitive.</summary>
    [Theory]
    [InlineData("low")]
    [InlineData("LOW")]
    [InlineData("Low")]
    public void TryParse_CaseInsensitive(string caseVariant)
    {
        bool parsed = TaskPriorities.TryParse(caseVariant, out TaskPriority result);
        Assert.True(parsed);
        Assert.Equal(TaskPriority.Low, result);
    }
}
