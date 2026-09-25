using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>Tests for <see cref="TaskDiff.Compare"/>: Spec §7.5, a field-by-field diff that
/// ignores Path, Version and ChangeLog.</summary>
public sealed class TaskDiffTests
{
    /// <summary>Two builds of the same Task, differing in nothing, produce no changes.</summary>
    [Fact]
    public void Compare_Identical_Empty()
    {
        TaskItem before = TestTasks.Make();
        TaskItem after = TestTasks.Make();

        IReadOnlyList<FieldChange> changes = TaskDiff.Compare(before, after);

        Assert.Empty(changes);
    }

    /// <summary>Path, Version and ChangeLog are explicitly ignored by Compare.</summary>
    [Fact]
    public void Compare_OnlyPathVersionChangeLogDiffer_Empty()
    {
        TaskItem before = TestTasks.Make(path: "a", version: "v1", changeLog: []);
        TaskItem after = TestTasks.Make(
            path: "b",
            version: "v2",
            changeLog: [TestTasks.Entry("2026-10-01T09:00:00Z", "Human", "created")]);

        IReadOnlyList<FieldChange> changes = TaskDiff.Compare(before, after);

        Assert.Empty(changes);
    }

    /// <summary>Each scalar field, changed alone, produces exactly one FieldChange carrying its
    /// wire text as Old and New.</summary>
    [Theory]
    [MemberData(nameof(ScalarFieldCases))]
    public void Compare_EachScalarField_OneChange(
        TaskItem before, TaskItem after, TaskField expectedField, string? expectedOld, string? expectedNew)
    {
        IReadOnlyList<FieldChange> changes = TaskDiff.Compare(before, after);

        FieldChange change = Assert.Single(changes);
        Assert.Equal(expectedField, change.Field);
        Assert.Equal(expectedOld, change.Old);
        Assert.Equal(expectedNew, change.New);
    }

    /// <summary>BlockedBy and Tags are compared as sets: reordering the same items produces no
    /// change.</summary>
    [Fact]
    public void Compare_BlockedByAndTags_OrderInsensitive()
    {
        _ = TaskId.TryParse("PLAT-0010", out TaskId a);
        _ = TaskId.TryParse("PLAT-0011", out TaskId b);

        TaskItem blockedByBefore = TestTasks.Make(blockedBy: [a, b]);
        TaskItem blockedByAfter = TestTasks.Make(blockedBy: [b, a]);

        TaskItem tagsBefore = TestTasks.Make(tags: ["alpha", "beta"]);
        TaskItem tagsAfter = TestTasks.Make(tags: ["beta", "alpha"]);

        Assert.Empty(TaskDiff.Compare(blockedByBefore, blockedByAfter));
        Assert.Empty(TaskDiff.Compare(tagsBefore, tagsAfter));
    }

    /// <summary>A changed Tags list reports Old and New as the stored-order items joined with
    /// "; ", which Summarise later splits.</summary>
    [Fact]
    public void Compare_TagsChanged_OldNewJoinedWithSemicolonSpace()
    {
        TaskItem before = TestTasks.Make(tags: ["a", "legacy"]);
        TaskItem after = TestTasks.Make(tags: ["a", "security"]);

        FieldChange change = Assert.Single(TaskDiff.Compare(before, after));

        Assert.Equal(TaskField.Tags, change.Field);
        Assert.Equal("a; legacy", change.Old);
        Assert.Equal("a; security", change.New);
    }

    /// <summary>A changed BlockedBy list reports Old and New as the stored-order items joined
    /// with "; ".</summary>
    [Fact]
    public void Compare_BlockedByChanged_OldNewJoinedWithSemicolonSpace()
    {
        _ = TaskId.TryParse("PLAT-0010", out TaskId a);
        _ = TaskId.TryParse("PLAT-0011", out TaskId b);

        TaskItem before = TestTasks.Make(blockedBy: [a]);
        TaskItem after = TestTasks.Make(blockedBy: [a, b]);

        FieldChange change = Assert.Single(TaskDiff.Compare(before, after));

        Assert.Equal(TaskField.BlockedBy, change.Field);
        Assert.Equal("PLAT-0010", change.Old);
        Assert.Equal("PLAT-0010; PLAT-0011", change.New);
    }

    /// <summary>A description change reports Field.Description with null Old and New: the text
    /// itself is never carried.</summary>
    [Fact]
    public void Compare_Description_OneChangeWithNullTexts()
    {
        TaskItem before = TestTasks.Make(description: "Before text.");
        TaskItem after = TestTasks.Make(description: "After text.");

        FieldChange change = Assert.Single(TaskDiff.Compare(before, after));

        Assert.Equal(TaskField.Description, change.Field);
        Assert.Null(change.Old);
        Assert.Null(change.New);
    }

    /// <summary>A Team or Project change reports one Location change with "Team" or
    /// "Team/Project" text; a Closed-only change is not a Location change.</summary>
    [Fact]
    public void Compare_LocationChanged_OneLocationChange()
    {
        TaskItem before = TestTasks.Make(location: new("Platform", null, false));
        TaskItem after = TestTasks.Make(location: new("Platform", "Auth v2", false));

        FieldChange change = Assert.Single(TaskDiff.Compare(before, after));

        Assert.Equal(TaskField.Location, change.Field);
        Assert.Equal("Platform", change.Old);
        Assert.Equal("Platform/Auth v2", change.New);

        TaskItem closedBefore = TestTasks.Make(location: new("Platform", null, false));
        TaskItem closedAfter = TestTasks.Make(location: new("Platform", null, true));

        Assert.Empty(TaskDiff.Compare(closedBefore, closedAfter));
    }

    /// <summary>A changed unknown field reports Field.Unknown with the key name as Old and null
    /// as New.</summary>
    [Fact]
    public void Compare_UnknownFieldChanged_OneUnknownChange()
    {
        TaskItem before = TestTasks.Make(unknownFields: [new("owner", "Alice")]);
        TaskItem after = TestTasks.Make(unknownFields: [new("owner", "Bob")]);

        FieldChange change = Assert.Single(TaskDiff.Compare(before, after));

        Assert.Equal(TaskField.Unknown, change.Field);
        Assert.Equal("owner", change.Old);
        Assert.Null(change.New);
    }

    /// <summary>Scenarios for <see cref="Compare_EachScalarField_OneChange"/>: one scalar field
    /// changed, everything else held constant.</summary>
    public static TheoryData<TaskItem, TaskItem, TaskField, string?, string?> ScalarFieldCases()
    {
        _ = TaskId.TryParse("PLAT-0002", out TaskId parentId);
        _ = TaskId.TryParse("PLAT-0003", out TaskId duplicateId);

        TheoryData<TaskItem, TaskItem, TaskField, string?, string?> data = new()
        {
            {
                TestTasks.Make(title: "Original"),
                TestTasks.Make(title: "Updated"),
                TaskField.Title, "Original", "Updated"
            },
            {
                TestTasks.Make(status: TaskState.ToDo),
                TestTasks.Make(status: TaskState.InProgress),
                TaskField.Status, "To Do", "In Progress"
            },
            {
                TestTasks.Make(priority: TaskPriority.Medium),
                TestTasks.Make(priority: TaskPriority.High),
                TaskField.Priority, "Medium", "High"
            },
            {
                TestTasks.Make(assignee: null),
                TestTasks.Make(assignee: "Nova"),
                TaskField.Assignee, null, "Nova"
            },
            {
                TestTasks.Make(originRoomId: null),
                TestTasks.Make(originRoomId: "room-1"),
                TaskField.Origin, null, "room-1"
            },
            {
                TestTasks.Make(parent: null),
                TestTasks.Make(parent: parentId),
                TaskField.Parent, null, "PLAT-0002"
            },
            {
                TestTasks.Make(duplicateOf: null),
                TestTasks.Make(duplicateOf: duplicateId),
                TaskField.DuplicateOf, null, "PLAT-0003"
            },
            {
                TestTasks.Make(startDate: null),
                TestTasks.Make(startDate: new DateOnly(2026, 10, 1)),
                TaskField.StartDate, null, "2026-10-01"
            },
            {
                TestTasks.Make(dueDate: null),
                TestTasks.Make(dueDate: new DateOnly(2026, 10, 15)),
                TaskField.DueDate, null, "2026-10-15"
            },
        };

        return data;
    }
}

/// <summary>Tests for <see cref="TaskDiff.Summarise"/>: the exact Spec §7.4 summary texts.</summary>
public sealed class TaskDiffSummariseTests
{
    /// <summary>A scalar status change reads "status: To Do → In Progress".</summary>
    [Fact]
    public void Summarise_StatusChange_ArrowText()
    {
        string text = TaskDiff.Summarise([new FieldChange(TaskField.Status, "To Do", "In Progress")]);

        Assert.Equal("status: To Do → In Progress", text);
    }

    /// <summary>An empty Old value reads as the em dash "—".</summary>
    [Fact]
    public void Summarise_AssigneeFromEmpty_UsesEmDash()
    {
        string text = TaskDiff.Summarise([new FieldChange(TaskField.Assignee, null, "Nova")]);

        Assert.Equal("assignee: — → Nova", text);
    }

    /// <summary>Tags changes list additions first, then removals, each group in ordinal order,
    /// using U+2212 for the minus sign.</summary>
    [Fact]
    public void Summarise_TagsChange_AddedThenRemoved()
    {
        string text = TaskDiff.Summarise([new FieldChange(TaskField.Tags, "a; legacy", "a; security")]);

        Assert.Equal("tags: +security, −legacy", text);
    }

    /// <summary>A Description change reads "description edited"; the text itself never appears.</summary>
    [Fact]
    public void Summarise_DescriptionChange_EditedOnly()
    {
        string text = TaskDiff.Summarise([new FieldChange(TaskField.Description, null, null)]);

        Assert.Equal("description edited", text);
    }

    /// <summary>A Location change reads "moved: {Old} → {New}".</summary>
    [Fact]
    public void Summarise_LocationChange_MovedText()
    {
        string text = TaskDiff.Summarise(
            [new FieldChange(TaskField.Location, "Platform/Auth v2", "Marketing")]);

        Assert.Equal("moved: Platform/Auth v2 → Marketing", text);
    }

    /// <summary>An unknown field change reads "{key} edited".</summary>
    [Fact]
    public void Summarise_UnknownFieldChange_KeyEditedText()
    {
        string text = TaskDiff.Summarise([new FieldChange(TaskField.Unknown, "owner", null)]);

        Assert.Equal("owner edited", text);
    }

    /// <summary>Two changes are joined with "; " in the input order.</summary>
    [Fact]
    public void Summarise_TwoChanges_JoinedInInputOrder()
    {
        string text = TaskDiff.Summarise(
        [
            new FieldChange(TaskField.Status, "To Do", "In Progress"),
            new FieldChange(TaskField.Assignee, null, "Nova"),
        ]);

        Assert.Equal("status: To Do → In Progress; assignee: — → Nova", text);
    }

    /// <summary>An empty change list summarises to the empty string.</summary>
    [Fact]
    public void Summarise_EmptyList_EmptyString()
    {
        string text = TaskDiff.Summarise([]);

        Assert.Equal("", text);
    }
}
