using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Tasks.Views;

namespace Agency.Huddle.Tests.Tasks.Views;

/// <summary>Tests that <see cref="TaskFilter.Sprints"/> leaves an existing <c>views.json</c> untouched (Sprints spec §11.1).</summary>
public sealed class ViewStoreSprintTests
{
    /// <summary>A View with no Sprint filter, using several existing filter dimensions.</summary>
    private static TaskView Plain() => new()
    {
        Id = "plain-1",
        Name = "Plain",
        Kind = ViewKind.List,
        Filter = new TaskFilter { Assignees = ["@me"], Teams = ["Platform"] },
    };

    /// <summary>A file with no Sprint filter loads and is saved back byte-identically, with no <c>sprints</c> key.</summary>
    [Fact]
    public void ExistingFile_NoSprintFilter_LoadsAndSavesByteIdentically()
    {
        using TempDataDir dataDir = new();
        string path = Path.Combine(dataDir.Path, "views.json");
        using (ViewStore first = new(dataDir.Options(), NullLogger<ViewStore>.Instance))
        {
            Assert.True(first.Save(Plain()).Saved);
        }

        byte[] before = File.ReadAllBytes(path);
        Assert.DoesNotContain("sprints", Encoding.UTF8.GetString(before), StringComparison.Ordinal);

        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);
        TaskView loaded = Assert.Single(store.Views, v => v.Id == "plain-1");
        Assert.Empty(loaded.Filter.Sprints);
        Assert.True(store.Save(loaded).Saved);

        Assert.Equal(before, File.ReadAllBytes(path));
    }

    /// <summary>A View filtering on Sprints writes a <c>sprints</c> array and reads it back.</summary>
    [Fact]
    public void SprintFilter_RoundTrips()
    {
        using TempDataDir dataDir = new();
        TaskView view = Plain() with { Filter = new TaskFilter { Sprints = ["@current", "S2"] } };

        using (ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance))
        {
            Assert.True(store.Save(view).Saved);
        }

        string text = File.ReadAllText(Path.Combine(dataDir.Path, "views.json"));
        Assert.Contains("\"sprints\"", text, StringComparison.Ordinal);
        using ViewStore reloaded = new(dataDir.Options(), NullLogger<ViewStore>.Instance);
        TaskView loaded = Assert.Single(reloaded.Views, v => v.Id == "plain-1");
        Assert.Equal(["@current", "S2"], loaded.Filter.Sprints);
    }

    /// <summary>A hand-written bad Sprint filter value marks the View invalid rather than loading it.</summary>
    [Fact]
    public void BadSprintValue_MarksViewInvalid()
    {
        using TempDataDir dataDir = new();
        File.WriteAllText(
            Path.Combine(dataDir.Path, "views.json"),
            "{ \"version\": 1, \"views\": [ { \"id\": \"x\", \"name\": \"X\", \"kind\": \"list\", \"filter\": { \"sprints\": [\"banana\"] } } ] }");

        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);

        Assert.DoesNotContain(store.Views, v => v.Id == "x");
        Assert.Contains(
            store.InvalidViews.SelectMany(v => v.Problems),
            p => p.Contains("'banana' is not a sprint id", StringComparison.Ordinal));
    }
}
