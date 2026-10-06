using Agency.Huddle.App.Tasks.Views;

namespace Agency.Huddle.Tests.Tasks.Views;

/// <summary>Tests for the Sprint rules of <see cref="ViewValidator"/> and <see cref="ViewFieldKeys"/> (Sprints spec §11.5).</summary>
public sealed class ViewValidatorSprintTests
{
    /// <summary>A List View with the given Sprint filter values.</summary>
    /// <param name="sprints">The filter values.</param>
    private static TaskView ListWith(params string[] sprints) => new()
    {
        Id = "v1",
        Name = "Sprinty",
        Kind = ViewKind.List,
        Filter = new TaskFilter { Sprints = sprints },
    };

    /// <summary>The two tokens and an id, in any case, are valid.</summary>
    [Fact]
    public void Validate_SprintTokensAndIds_Valid() =>
        Assert.Empty(ViewValidator.Validate(ListWith("@current", "@NONE", "S2", "s12"), []));

    /// <summary>Anything else names the offending value.</summary>
    [Fact]
    public void Validate_BadSprintValue_Refused() =>
        Assert.Equal(
            ["Sprint filter value 'banana' is not a sprint id, @current or @none."],
            ViewValidator.Validate(ListWith("S1", "banana"), []));

    /// <summary><c>sprint</c> is a known field and is sortable.</summary>
    [Fact]
    public void Validate_SprintFieldAndSort_Valid()
    {
        TaskView view = ListWith() with { Fields = ["sprint"], Sort = [new SortKey("sprint", SortDirection.Descending)] };

        Assert.Empty(ViewValidator.Validate(view, []));
        Assert.Contains("sprint", (IEnumerable<string>)ViewFieldKeys.All);
        Assert.Contains("sprint", (IEnumerable<string>)ViewFieldKeys.Sortable);
    }

    /// <summary>A Board may group by Sprint.</summary>
    [Fact]
    public void Validate_BoardGroupedBySprint_Valid()
    {
        TaskView view = new()
        {
            Id = "b",
            Name = "B",
            Kind = ViewKind.Board,
            Grouping = [TaskGroupField.Sprint],
            Columns = BoardLayout.DefaultColumns,
        };

        Assert.Empty(ViewValidator.Validate(view, []));
    }
}
