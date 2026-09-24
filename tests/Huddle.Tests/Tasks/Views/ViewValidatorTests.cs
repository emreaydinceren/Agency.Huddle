using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;

namespace Agency.Huddle.Tests.Tasks.Views;

/// <summary>Tests for <see cref="ViewValidator"/> (Spec §12.4).</summary>
public sealed class ViewValidatorTests
{
    /// <summary>A minimal valid List View, used as the baseline for each rule's negative case.</summary>
    private static TaskView ValidList() => new()
    {
        Id = "v1",
        Name = "My List",
        Kind = ViewKind.List,
    };

    /// <summary>A minimal valid Board View whose columns cover every state exactly once.</summary>
    private static TaskView ValidBoard() => new()
    {
        Id = "v1",
        Name = "My Board",
        Kind = ViewKind.Board,
        Columns =
        [
            new BoardColumn("Backlog", [TaskState.Backlog]),
            new BoardColumn("To Do", [TaskState.ToDo]),
            new BoardColumn("In Progress", [TaskState.InProgress]),
            new BoardColumn("Review", [TaskState.Review]),
            new BoardColumn("Done", [TaskState.Done]),
            new BoardColumn("Won't do", [TaskState.Cancelled, TaskState.Duplicate, TaskState.Rejected]),
        ],
    };

    /// <summary>A valid View has no problems.</summary>
    [Fact]
    public void Validate_ValidList_NoProblems() =>
        Assert.Empty(ViewValidator.Validate(ValidList(), []));

    /// <summary>A valid Board has no problems.</summary>
    [Fact]
    public void Validate_ValidBoard_NoProblems() =>
        Assert.Empty(ViewValidator.Validate(ValidBoard(), []));

    /// <summary>An empty name is a problem.</summary>
    [Fact]
    public void Validate_EmptyName_HasProblem()
    {
        TaskView view = ValidList() with { Name = "" };

        Assert.NotEmpty(ViewValidator.Validate(view, []));
    }

    /// <summary>A name of 61 characters is a problem.</summary>
    [Fact]
    public void Validate_NameTooLong_HasProblem()
    {
        TaskView view = ValidList() with { Name = new string('a', 61) };

        Assert.NotEmpty(ViewValidator.Validate(view, []));
    }

    /// <summary>A name that duplicates an existing View's, ignoring case, is a problem.</summary>
    [Fact]
    public void Validate_DuplicateName_HasProblem()
    {
        TaskView existing = ValidList() with { Id = "other", Name = "MY LIST" };
        TaskView view = ValidList();

        Assert.NotEmpty(ViewValidator.Validate(view, [existing]));
    }

    /// <summary>An unknown field key is a problem.</summary>
    [Fact]
    public void Validate_UnknownFieldKey_HasProblem()
    {
        TaskView view = ValidList() with { Fields = ["not_a_field"] };

        Assert.NotEmpty(ViewValidator.Validate(view, []));
    }

    /// <summary>A duplicate grouping field is a problem.</summary>
    [Fact]
    public void Validate_DuplicateGrouping_HasProblem()
    {
        TaskView view = ValidList() with { Grouping = [TaskGroupField.Team, TaskGroupField.Team] };

        Assert.NotEmpty(ViewValidator.Validate(view, []));
    }

    /// <summary>A Board grouped by State is a problem.</summary>
    [Fact]
    public void Validate_BoardWithStateGrouping_HasProblem()
    {
        TaskView view = ValidBoard() with { Grouping = [TaskGroupField.State] };

        Assert.NotEmpty(ViewValidator.Validate(view, []));
    }

    /// <summary>A Board with scope Closed is a problem.</summary>
    [Fact]
    public void Validate_BoardWithClosedScope_HasProblem()
    {
        TaskView view = ValidBoard() with { Scope = ViewScope.Closed };

        Assert.NotEmpty(ViewValidator.Validate(view, []));
    }

    /// <summary>A Board missing Review from every column is a problem.</summary>
    [Fact]
    public void Validate_BoardMissingReview_HasProblem()
    {
        TaskView view = ValidBoard() with
        {
            Columns =
            [
                new BoardColumn("Backlog", [TaskState.Backlog]),
                new BoardColumn("To Do", [TaskState.ToDo]),
                new BoardColumn("In Progress", [TaskState.InProgress]),
                new BoardColumn("Done", [TaskState.Done]),
                new BoardColumn("Won't do", [TaskState.Cancelled, TaskState.Duplicate, TaskState.Rejected]),
            ],
        };

        Assert.NotEmpty(ViewValidator.Validate(view, []));
    }

    /// <summary>A Board with Done in two columns is a problem.</summary>
    [Fact]
    public void Validate_BoardWithDoneInTwoColumns_HasProblem()
    {
        TaskView view = ValidBoard() with
        {
            Columns =
            [
                new BoardColumn("Backlog", [TaskState.Backlog]),
                new BoardColumn("To Do", [TaskState.ToDo]),
                new BoardColumn("In Progress", [TaskState.InProgress]),
                new BoardColumn("Review", [TaskState.Review]),
                new BoardColumn("Done", [TaskState.Done]),
                new BoardColumn("Also Done", [TaskState.Done, TaskState.Cancelled, TaskState.Duplicate, TaskState.Rejected]),
            ],
        };

        Assert.NotEmpty(ViewValidator.Validate(view, []));
    }

    /// <summary>A column with no states is a problem.</summary>
    [Fact]
    public void Validate_ColumnWithNoStates_HasProblem()
    {
        TaskView view = ValidBoard() with
        {
            Columns =
            [
                new BoardColumn("Backlog", [TaskState.Backlog]),
                new BoardColumn("To Do", [TaskState.ToDo]),
                new BoardColumn("In Progress", [TaskState.InProgress]),
                new BoardColumn("Review", [TaskState.Review]),
                new BoardColumn("Done", [TaskState.Done]),
                new BoardColumn("Empty", []),
                new BoardColumn("Won't do", [TaskState.Cancelled, TaskState.Duplicate, TaskState.Rejected]),
            ],
        };

        Assert.NotEmpty(ViewValidator.Validate(view, []));
    }

    /// <summary>A column label of 31 characters is a problem.</summary>
    [Fact]
    public void Validate_ColumnLabelTooLong_HasProblem()
    {
        TaskView view = ValidBoard() with
        {
            Columns =
            [
                new BoardColumn(new string('a', 31), [TaskState.Backlog]),
                new BoardColumn("To Do", [TaskState.ToDo]),
                new BoardColumn("In Progress", [TaskState.InProgress]),
                new BoardColumn("Review", [TaskState.Review]),
                new BoardColumn("Done", [TaskState.Done]),
                new BoardColumn("Won't do", [TaskState.Cancelled, TaskState.Duplicate, TaskState.Rejected]),
            ],
        };

        Assert.NotEmpty(ViewValidator.Validate(view, []));
    }

    /// <summary>A List with columns is a problem.</summary>
    [Fact]
    public void Validate_ListWithColumns_HasProblem()
    {
        TaskView view = ValidList() with { Columns = [new BoardColumn("Backlog", [TaskState.Backlog])] };

        Assert.NotEmpty(ViewValidator.Validate(view, []));
    }

    /// <summary>Sorting by tags, an unsortable field, is a problem.</summary>
    [Fact]
    public void Validate_SortByTags_HasProblem()
    {
        TaskView view = ValidList() with { Sort = [new SortKey("tags", SortDirection.Ascending)] };

        Assert.NotEmpty(ViewValidator.Validate(view, []));
    }

    /// <summary>A duplicate sort field is a problem.</summary>
    [Fact]
    public void Validate_DuplicateSortField_HasProblem()
    {
        TaskView view = ValidList() with
        {
            Sort =
            [
                new SortKey("priority", SortDirection.Ascending),
                new SortKey("priority", SortDirection.Descending),
            ],
        };

        Assert.NotEmpty(ViewValidator.Validate(view, []));
    }
}
