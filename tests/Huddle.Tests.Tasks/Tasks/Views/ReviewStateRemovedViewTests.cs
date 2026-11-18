using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Tasks.Views;

/// <summary>
/// Slice 1 of #669 (decisions D-6 and D-11): a saved View written while Huddle had a Review state loses that state
/// when it is read, and a new Board has no Review column. The tests name the state by its wire text only, because
/// the enum member does not exist once the slice is built.
/// </summary>
public sealed class ReviewStateRemovedViewTests
{
    /// <summary>Writes a <c>views.json</c> holding one Board View with <paramref name="columnsJson"/>.</summary>
    private static void WriteBoardFile(TempDataDir dataDir, string columnsJson) =>
        File.WriteAllText(
            Path.Combine(dataDir.Path, "views.json"),
            "{ \"version\": 1, \"views\": [ { \"id\": \"b1\", \"name\": \"B\", \"kind\": \"kanban\", \"columns\": [ " + columnsJson + " ] } ] }");

    /// <summary>Writes a <c>views.json</c> holding one list View whose state filter is <paramref name="statesJson"/>.</summary>
    private static void WriteListFile(TempDataDir dataDir, string statesJson) =>
        File.WriteAllText(
            Path.Combine(dataDir.Path, "views.json"),
            "{ \"version\": 1, \"views\": [ { \"id\": \"v1\", \"name\": \"V\", \"kind\": \"list\", \"filter\": { \"states\": " + statesJson + " } } ] }");

    /// <summary>The wire names of every state any column of <paramref name="view"/> holds.</summary>
    private static List<string> ColumnStateWires(TaskView view) =>
        [.. view.Columns.SelectMany(static column => column.States).Select(static state => state.ToWire())];

    /// <summary>A Board saved with its own Review column reads with no Review state, the other columns intact, and still validates (D-6).</summary>
    [Fact]
    public void Read_BoardWithReviewColumn_HasNoReviewStateAndKeepsTheOtherColumns()
    {
        using TempDataDir dataDir = new();
        WriteBoardFile(
            dataDir,
            """
            { "label": "Backlog", "states": ["Backlog"] }, { "label": "To Do", "states": ["To Do"] },
            { "label": "In Progress", "states": ["In Progress"] }, { "label": "Review", "states": ["Review"] },
            { "label": "Done", "states": ["Done"] },
            { "label": "Won't do", "states": ["Cancelled", "Duplicate", "Rejected"] }
            """);

        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);

        TaskView view = Assert.Single(store.Views, v => v.Id == "b1");
        Assert.DoesNotContain("Review", ColumnStateWires(view));
        Assert.DoesNotContain("Review", view.Columns.Select(static column => column.Label));
        Assert.Equal(
            ["Backlog", "To Do", "In Progress", "Done", "Won't do"],
            view.Columns.Select(static column => column.Label).Where(static label => label != "Needs You").ToList());
        Assert.Empty(store.InvalidViews);
        Assert.Empty(ViewValidator.Validate(view, store.Views.Where(v => v.Id != "b1").ToList()));
    }

    /// <summary>A Review state that shares a column is dropped from it and the rest of that column stays (D-6).</summary>
    [Fact]
    public void Read_BoardWithReviewInSharedColumn_KeepsTheOtherStatesOfThatColumn()
    {
        using TempDataDir dataDir = new();
        WriteBoardFile(
            dataDir,
            """
            { "label": "Open", "states": ["Backlog", "To Do", "In Progress", "Review"] },
            { "label": "Done", "states": ["Done"] },
            { "label": "Won't do", "states": ["Cancelled", "Duplicate", "Rejected"] }
            """);

        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);

        TaskView view = Assert.Single(store.Views, v => v.Id == "b1");
        Assert.DoesNotContain("Review", ColumnStateWires(view));
        BoardColumn open = Assert.Single(view.Columns, static column => column.Label == "Open");
        Assert.Equal(["Backlog", "To Do", "In Progress"], open.States.Select(static state => state.ToWire()).ToList());
        Assert.Empty(store.InvalidViews);
    }

    /// <summary>A state filter that names Review drops it and keeps the other states, and the View still loads (D-6).</summary>
    [Fact]
    public void Read_ListViewFilterNamingReview_DropsReviewAndKeepsTheRest()
    {
        using TempDataDir dataDir = new();
        WriteListFile(dataDir, "[\"To Do\", \"Review\", \"Done\"]");

        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);

        TaskView view = Assert.Single(store.Views, v => v.Id == "v1");
        List<string> wires = [.. view.Filter.States.Select(static state => state.ToWire())];
        Assert.DoesNotContain("Review", wires);
        Assert.Contains("To Do", wires);
        Assert.Contains("Done", wires);
        Assert.Empty(store.InvalidViews);
    }

    /// <summary>A new Board has Backlog, To Do, In Progress, Needs You and Done, keeps the Won't do column, and has no Review column (D-11).</summary>
    [Fact]
    public void DefaultColumns_AreTheFiveWorkColumnsPlusWontDo_WithoutReview()
    {
        string[] labels = [.. BoardLayout.DefaultColumns.Select(static column => column.Label)];

        Assert.Equal(["Backlog", "To Do", "In Progress", "Needs You", "Done", "Won't do"], labels);
        Assert.DoesNotContain("Review", BoardLayout.DefaultColumns.SelectMany(static column => column.States).Select(static state => state.ToWire()));
    }
}
