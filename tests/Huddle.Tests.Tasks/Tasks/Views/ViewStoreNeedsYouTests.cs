using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;

namespace Agency.Huddle.Tests.Tasks.Views;

/// <summary>Tests that <see cref="ViewStore"/> adds Needs You to a state filter that has Review, in memory only (Human gate Spec §6.2, Views).</summary>
public sealed class ViewStoreNeedsYouTests
{
    /// <summary>Writes a <c>views.json</c> holding one list View whose state filter is <paramref name="statesJson"/>.</summary>
    /// <param name="dataDir">The data directory.</param>
    /// <param name="statesJson">The JSON array text of the states.</param>
    /// <returns>The file path.</returns>
    private static string WriteViewsFile(TempDataDir dataDir, string statesJson)
    {
        string path = Path.Combine(dataDir.Path, "views.json");
        File.WriteAllText(
            path,
            "{ \"version\": 1, \"views\": [ { \"id\": \"v1\", \"name\": \"V\", \"kind\": \"list\", \"filter\": { \"states\": " + statesJson + " } } ] }");
        return path;
    }

    /// <summary>A View whose state filter has Review reads with Needs You added.</summary>
    [Fact]
    public void Read_ViewWithReview_GainsNeedsYou()
    {
        using TempDataDir dataDir = new();
        WriteViewsFile(dataDir, "[\"To Do\", \"Review\"]");

        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);

        TaskView view = Assert.Single(store.Views, v => v.Id == "v1");
        Assert.Contains(TaskState.Review, view.Filter.States);
        Assert.Contains(TaskState.NeedsYou, view.Filter.States);
    }

    /// <summary>A View whose state filter has no Review is left exactly as written.</summary>
    [Fact]
    public void Read_ViewWithoutReview_IsUnchanged()
    {
        using TempDataDir dataDir = new();
        WriteViewsFile(dataDir, "[\"To Do\", \"In Progress\"]");

        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);

        TaskView view = Assert.Single(store.Views, v => v.Id == "v1");
        Assert.Equal([TaskState.ToDo, TaskState.InProgress], view.Filter.States);
    }

    /// <summary>A View that already has Needs You does not gain a second one.</summary>
    [Fact]
    public void Read_ViewWithReviewAndNeedsYou_HasNeedsYouOnce()
    {
        using TempDataDir dataDir = new();
        WriteViewsFile(dataDir, "[\"Review\", \"Needs You\"]");

        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);

        TaskView view = Assert.Single(store.Views, v => v.Id == "v1");
        Assert.Single(view.Filter.States, s => s == TaskState.NeedsYou);
    }

    /// <summary>Reading the gained state never rewrites <c>views.json</c>: its bytes on disk are unchanged.</summary>
    [Fact]
    public void Read_ViewGainsNeedsYou_FileIsNotRewritten()
    {
        using TempDataDir dataDir = new();
        string path = WriteViewsFile(dataDir, "[\"Review\"]");
        byte[] before = File.ReadAllBytes(path);

        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);

        TaskView view = Assert.Single(store.Views, v => v.Id == "v1");
        Assert.Contains(TaskState.NeedsYou, view.Filter.States);
        Assert.Equal(before, File.ReadAllBytes(path));
    }
}
