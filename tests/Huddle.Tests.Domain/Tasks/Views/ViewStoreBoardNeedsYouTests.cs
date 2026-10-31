using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;

namespace Agency.Huddle.Tests.Tasks.Views;

/// <summary>Tests that <see cref="ViewStore"/> gives a saved Board View a Needs You column, in memory only (Human gate Spec §6.2, Views).</summary>
public sealed class ViewStoreBoardNeedsYouTests
{
    /// <summary>A six-column saved Board, as written before the Needs You state existed.</summary>
    private const string SixColumns = """
        { "label": "Backlog", "states": ["Backlog"] }, { "label": "To Do", "states": ["To Do"] },
        { "label": "In Progress", "states": ["In Progress"] }, { "label": "Review", "states": ["Review"] },
        { "label": "Done", "states": ["Done"] },
        { "label": "Won't do", "states": ["Cancelled", "Duplicate", "Rejected"] }
        """;

    /// <summary>Writes a <c>views.json</c> holding one Board View with <paramref name="columnsJson"/>.</summary>
    /// <param name="dataDir">The data directory.</param>
    /// <param name="columnsJson">The JSON text of the column objects.</param>
    /// <returns>The file path.</returns>
    private static string WriteBoardFile(TempDataDir dataDir, string columnsJson)
    {
        string path = Path.Combine(dataDir.Path, "views.json");
        File.WriteAllText(
            path,
            "{ \"version\": 1, \"views\": [ { \"id\": \"b1\", \"name\": \"B\", \"kind\": \"kanban\", \"columns\": [ " + columnsJson + " ] } ] }");
        return path;
    }

    /// <summary>A Board with a Review column gains Needs You right after it.</summary>
    [Fact]
    public void Read_BoardWithReview_GainsNeedsYouAfterReview()
    {
        using TempDataDir dataDir = new();
        WriteBoardFile(dataDir, SixColumns);

        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);

        TaskView view = Assert.Single(store.Views, v => v.Id == "b1");
        Assert.Equal(
            ["Backlog", "To Do", "In Progress", "Review", "Needs You", "Done", "Won't do"],
            view.Columns.Select(c => c.Label).ToList());
        Assert.Equal([TaskState.NeedsYou], view.Columns[4].States);
    }

    /// <summary>A Board whose Review shares a column with other states gains Needs You right after that column, before Done.</summary>
    [Fact]
    public void Read_BoardWithReviewInSharedColumn_GainsNeedsYouAfterIt()
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
        Assert.Equal(["Open", "Needs You", "Done", "Won't do"], view.Columns.Select(c => c.Label).ToList());
    }

    /// <summary>A Board that already has a Needs You column is left exactly as written.</summary>
    [Fact]
    public void Read_BoardWithNeedsYou_IsUnchanged()
    {
        using TempDataDir dataDir = new();
        WriteBoardFile(
            dataDir,
            """
            { "label": "Open", "states": ["Backlog", "To Do", "In Progress", "Review"] },
            { "label": "Waiting", "states": ["Needs You"] },
            { "label": "Done", "states": ["Done"] },
            { "label": "Won't do", "states": ["Cancelled", "Duplicate", "Rejected"] }
            """);

        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);

        TaskView view = Assert.Single(store.Views, v => v.Id == "b1");
        Assert.Equal(["Open", "Waiting", "Done", "Won't do"], view.Columns.Select(c => c.Label).ToList());
    }

    /// <summary>Reading the gained column never rewrites <c>views.json</c>: its bytes on disk are unchanged.</summary>
    [Fact]
    public void Read_BoardGainsNeedsYou_FileIsNotRewritten()
    {
        using TempDataDir dataDir = new();
        string path = WriteBoardFile(dataDir, SixColumns);
        byte[] before = File.ReadAllBytes(path);

        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);

        TaskView view = Assert.Single(store.Views, v => v.Id == "b1");
        Assert.Equal(7, view.Columns.Count);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    /// <summary>The upgraded Board is a valid View: it loads rather than landing in the invalid list, and the validator accepts it.</summary>
    [Fact]
    public void Read_BoardGainsNeedsYou_PassesTheValidator()
    {
        using TempDataDir dataDir = new();
        WriteBoardFile(dataDir, SixColumns);

        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);

        TaskView view = Assert.Single(store.Views, v => v.Id == "b1");
        Assert.Empty(store.InvalidViews);
        Assert.Empty(ViewValidator.Validate(view, store.Views.Where(v => v.Id != "b1").ToList()));
    }
}
