using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>Tests for <see cref="TaskItem"/> derived properties.</summary>
public sealed class TaskItemTests
{
    /// <summary>Created is the At of the first ChangeLog entry.</summary>
    [Fact]
    public void Created_IsFirstEntryAt()
    {
        DateTimeOffset first = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        DateTimeOffset second = new(2026, 10, 2, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset third = new(2026, 10, 3, 11, 0, 0, TimeSpan.Zero);

        TaskItem task = new()
        {
            Id = new("PLAT", 1),
            Title = "Test",
            Status = TaskState.ToDo,
            Priority = TaskPriority.Medium,
            Creator = "Human",
            Location = new("Platform", null, false),
            Path = "",
            Version = "",
            ChangeLog =
            [
                new(first, "Human", "created"),
                new(second, "Agent", "updated"),
                new(third, "Human", "changed"),
            ],
        };

        Assert.Equal(first, task.Created);
    }

    /// <summary>Updated is the At of the last ChangeLog entry.</summary>
    [Fact]
    public void Updated_IsLastEntryAt()
    {
        DateTimeOffset first = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        DateTimeOffset second = new(2026, 10, 2, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset third = new(2026, 10, 3, 11, 0, 0, TimeSpan.Zero);

        TaskItem task = new()
        {
            Id = new("PLAT", 1),
            Title = "Test",
            Status = TaskState.ToDo,
            Priority = TaskPriority.Medium,
            Creator = "Human",
            Location = new("Platform", null, false),
            Path = "",
            Version = "",
            ChangeLog =
            [
                new(first, "Human", "created"),
                new(second, "Agent", "updated"),
                new(third, "Human", "changed"),
            ],
        };

        Assert.Equal(third, task.Updated);
    }

    /// <summary>Created and Updated are null when there are no ChangeLog entries.</summary>
    [Fact]
    public void CreatedAndUpdated_NoEntries_AreNull()
    {
        TaskItem task = new()
        {
            Id = new("PLAT", 1),
            Title = "Test",
            Status = TaskState.ToDo,
            Priority = TaskPriority.Medium,
            Creator = "Human",
            Location = new("Platform", null, false),
            Path = "",
            Version = "",
            ChangeLog = [],
        };

        Assert.Null(task.Created);
        Assert.Null(task.Updated);
    }

    /// <summary>Created and Updated work correctly with a single ChangeLog entry.</summary>
    [Fact]
    public void CreatedAndUpdated_SingleEntry_AreSame()
    {
        DateTimeOffset only = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

        TaskItem task = new()
        {
            Id = new("PLAT", 1),
            Title = "Test",
            Status = TaskState.ToDo,
            Priority = TaskPriority.Medium,
            Creator = "Human",
            Location = new("Platform", null, false),
            Path = "",
            Version = "",
            ChangeLog =
            [
                new(only, "Human", "created"),
            ],
        };

        Assert.Equal(only, task.Created);
        Assert.Equal(only, task.Updated);
    }
}
