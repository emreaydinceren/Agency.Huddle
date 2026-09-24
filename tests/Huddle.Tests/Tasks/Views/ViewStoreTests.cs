using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Tasks.Views;

namespace Agency.Huddle.Tests.Tasks.Views;

/// <summary>
/// Tests for <see cref="ViewStore"/>: Spec §12.3's four deliberate differences from
/// <see cref="Agency.Huddle.App.Avatars.AvatarStore"/> (a malformed file sets <c>LoadError</c> and
/// refuses every write rather than falling back to empty, writes go through a temp file plus an
/// atomic move, an invalid View entry is kept and flagged rather than skipped, and the two built-in
/// Views are created in memory and only written once edited), and Spec §9.6's
/// <see cref="ViewStore.RenameTeammate(string, string)"/>.
/// </summary>
public sealed class ViewStoreTests
{
    /// <summary>A View good enough to pass <see cref="ViewValidator"/> with no columns or grouping to worry about.</summary>
    /// <param name="id">The View's id.</param>
    /// <param name="name">The View's name.</param>
    /// <param name="assignees">The assignee filter values. Default: none.</param>
    private static TaskView ListView(string id, string name, IReadOnlyList<string>? assignees = null)
    {
        return new TaskView
        {
            Id = id,
            Name = name,
            Kind = ViewKind.List,
            Filter = new TaskFilter { Assignees = assignees ?? [] },
        };
    }

    /// <summary>No file is the normal first-run case: the two built-ins are in memory and nothing is written just to read them.</summary>
    [Fact]
    public void NoFile_BuiltInsOnly_FileNotCreated()
    {
        using TempDataDir dataDir = new();
        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);

        Assert.Equal(2, store.Views.Count);
        Assert.Contains(store.Views, v => v.Id == "all-tasks" && v.BuiltIn);
        TaskView myTasks = Assert.Single(store.Views, v => v.Id == "my-tasks");
        Assert.True(myTasks.BuiltIn);
        Assert.Contains("@me", myTasks.Filter.Assignees);
        Assert.False(File.Exists(Path.Combine(dataDir.Path, "views.json")));
    }

    /// <summary>
    /// A malformed file reports its <see cref="ViewLoadError"/> with a 1-based line and column,
    /// computed independently here from <see cref="JsonNode.Parse(string, JsonNodeOptions?, JsonDocumentOptions)"/>'s
    /// own (0-based) <see cref="JsonException"/> rather than by re-deriving the store's
    /// arithmetic, and every write is refused with the file left byte-identical.
    /// </summary>
    [Fact]
    public void MalformedFile_LoadErrorWithLineAndColumn_SaveRefused_FileByteIdentical()
    {
        using TempDataDir dataDir = new();
        string path = Path.Combine(dataDir.Path, "views.json");
        const string malformed = "{\n  \"version\": 1,\n  \"views\": [\n    { \"id\": \"x\", \"name\": \"Bad\" broken\n";
        File.WriteAllText(path, malformed);

        JsonException? expected = null;
        try
        {
            JsonNode.Parse(malformed);
        }
        catch (JsonException ex)
        {
            expected = ex;
        }

        Assert.NotNull(expected);

        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);

        Assert.NotNull(store.LoadError);
        Assert.Equal((expected!.LineNumber ?? 0) + 1, store.LoadError!.Line);
        Assert.Equal((expected.BytePositionInLine ?? 0) + 1, store.LoadError.Column);

        ViewSaveResult result = store.Save(ListView("custom-1", "Custom View"));

        Assert.False(result.Saved);
        Assert.NotEmpty(result.Problems);
        Assert.Equal(malformed, File.ReadAllText(path));
    }

    /// <summary>
    /// Pins the 1-based-ness of <see cref="ViewLoadError"/> with a literal expected value: an empty
    /// file has consumed no input at all when <see cref="JsonNode.Parse(string, JsonNodeOptions?, JsonDocumentOptions)"/>
    /// gives up, so its raw <see cref="JsonException"/> reports the 0-based (0, 0) - and
    /// <see cref="ViewLoadError"/> must report the 1-based (1, 1), never that raw (0, 0).
    /// </summary>
    [Fact]
    public void MalformedFile_LoadError_IsOneBased()
    {
        using TempDataDir dataDir = new();
        File.WriteAllText(Path.Combine(dataDir.Path, "views.json"), string.Empty);

        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);

        Assert.NotNull(store.LoadError);
        Assert.Equal(1, store.LoadError!.Line);
        Assert.Equal(1, store.LoadError.Column);
    }

    /// <summary>One malformed entry in an otherwise well-formed file is kept and flagged with a reason; the well-formed entries beside it still load.</summary>
    [Fact]
    public void InvalidEntry_KeptAndFlagged_OthersLoad()
    {
        using TempDataDir dataDir = new();
        string path = Path.Combine(dataDir.Path, "views.json");
        const string json = """
            {
              "version": 1,
              "views": [
                { "id": "good-1", "name": "Good View", "kind": "list" },
                { "id": "bad-1", "name": "Bad View", "kind": "nonsense" }
              ]
            }
            """;
        File.WriteAllText(path, json);

        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);

        Assert.Contains(store.Views, v => v.Id == "good-1");
        Assert.DoesNotContain(store.Views, v => v.Id == "bad-1");
        InvalidView invalid = Assert.Single(store.InvalidViews);
        Assert.Equal("bad-1", invalid.Id);
        Assert.NotEmpty(invalid.Problems);
    }

    /// <summary>
    /// Saving an invalid entry back writes its raw JSON unchanged - including a field <see cref="TaskView"/>
    /// doesn't even know about - so a save can never quietly delete the Human's hand-edited entry.
    /// </summary>
    [Fact]
    public void Save_WithInvalidEntry_WritesItsRawJsonBack()
    {
        using TempDataDir dataDir = new();
        string path = Path.Combine(dataDir.Path, "views.json");
        const string json = """
            {
              "version": 1,
              "views": [
                { "id": "bad-1", "name": "Bad View", "kind": "nonsense", "customField": "keepme" }
              ]
            }
            """;
        File.WriteAllText(path, json);
        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);

        ViewSaveResult result = store.Save(ListView("good-1", "Good View"));

        Assert.True(result.Saved);
        JsonNode? node = JsonNode.Parse(File.ReadAllText(path));
        JsonArray views = node!["views"]!.AsArray();
        JsonObject badEntry = views.Select(entry => entry!.AsObject()).Single(entry => entry["id"]!.GetValue<string>() == "bad-1");
        Assert.Equal("nonsense", badEntry["kind"]!.GetValue<string>());
        Assert.Equal("keepme", badEntry["customField"]!.GetValue<string>());
    }

    /// <summary>A save leaves no temp file behind, and raises <see cref="ViewStore.ViewsChanged"/> exactly once by the time it returns.</summary>
    [Fact]
    public void Save_AtomicNoTmpLeft_RaisesViewsChanged()
    {
        using TempDataDir dataDir = new();
        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);
        int changedCount = 0;
        store.ViewsChanged += () => changedCount++;

        ViewSaveResult result = store.Save(ListView("custom-1", "Custom View"));

        Assert.True(result.Saved);
        Assert.Equal(1, changedCount);
        Assert.True(File.Exists(Path.Combine(dataDir.Path, "views.json")));
        IEnumerable<string> leftoverTmp = Directory.GetFiles(dataDir.Path).Where(f => f.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(leftoverTmp);
    }

    /// <summary>A built-in View is only ever written once it is edited; editing it writes the file and the edit sticks.</summary>
    [Fact]
    public void Save_EditsBuiltIn_WritesFile()
    {
        using TempDataDir dataDir = new();
        string path = Path.Combine(dataDir.Path, "views.json");
        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);
        Assert.False(File.Exists(path));
        TaskView? allTasks = store.Get("all-tasks");
        Assert.NotNull(allTasks);

        ViewSaveResult result = store.Save(allTasks! with { Name = "Everything" });

        Assert.True(result.Saved);
        Assert.True(File.Exists(path));
        Assert.Contains("Everything", File.ReadAllText(path), StringComparison.Ordinal);
        Assert.Equal("Everything", store.Get("all-tasks")!.Name);
    }

    /// <summary>Deleting a built-in View is refused, and it is still there afterward.</summary>
    [Fact]
    public void Delete_BuiltIn_Refused()
    {
        using TempDataDir dataDir = new();
        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);

        ViewSaveResult result = store.Delete("all-tasks");

        Assert.False(result.Saved);
        Assert.NotEmpty(result.Problems);
        Assert.Contains(store.Views, v => v.Id == "all-tasks");
    }

    /// <summary>An external edit to <c>views.json</c> is picked up by the watcher and reflected in <see cref="ViewStore.Views"/>.</summary>
    [Fact]
    public async Task ExternalEdit_Reloads()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        string path = Path.Combine(dataDir.Path, "views.json");
        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);

        TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        store.ViewsChanged += () => tcs.TrySetResult();

        const string json = """
            {
              "version": 1,
              "views": [
                { "id": "ext-1", "name": "External View", "kind": "list" }
              ]
            }
            """;
        await File.WriteAllTextAsync(path, json, ct);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        await using CancellationTokenRegistration registration = cts.Token.Register(() => tcs.TrySetCanceled());
        await tcs.Task;

        Assert.Contains(store.Views, v => v.Id == "ext-1");
    }

    /// <summary>
    /// A malformed file sets <see cref="ViewStore.LoadError"/>; once a later external write parses
    /// cleanly, the watcher's rebuild clears it.
    /// </summary>
    [Fact]
    public async Task ExternalFix_ClearsLoadError()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        string path = Path.Combine(dataDir.Path, "views.json");
        File.WriteAllText(path, "not json");
        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);
        Assert.NotNull(store.LoadError);

        TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        store.ViewsChanged += () => tcs.TrySetResult();

        const string fixedJson = """
            {
              "version": 1,
              "views": []
            }
            """;
        await File.WriteAllTextAsync(path, fixedJson, ct);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        await using CancellationTokenRegistration registration = cts.Token.Register(() => tcs.TrySetCanceled());
        await tcs.Task;

        Assert.Null(store.LoadError);
    }

    /// <summary>Saving a View again under its own Id - a rename or any other edit - is never flagged as a duplicate of itself.</summary>
    [Fact]
    public void Save_RenamedViewKeepsItsName_NotADuplicate()
    {
        using TempDataDir dataDir = new();
        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);
        ViewSaveResult first = store.Save(ListView("custom-1", "Sprint Board"));
        Assert.True(first.Saved);

        ViewSaveResult second = store.Save(ListView("custom-1", "Sprint Board") with { Description = "Updated" });

        Assert.True(second.Saved);
        Assert.Empty(second.Problems);
        Assert.Equal("Updated", store.Get("custom-1")!.Description);
    }

    /// <summary><see cref="ViewStore.RenameTeammate(string, string)"/> rewrites every View's assignee filter that named the old Teammate, leaving other assignee values untouched.</summary>
    [Fact]
    public void RenameTeammate_RewritesAssigneeFilters()
    {
        using TempDataDir dataDir = new();
        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);
        store.Save(ListView("custom-1", "Custom View", assignees: ["Nova", "@unassigned"]));

        store.RenameTeammate("Nova", "NovaPrime");

        TaskView? updated = store.Get("custom-1");
        Assert.NotNull(updated);
        Assert.Contains("NovaPrime", updated!.Filter.Assignees);
        Assert.DoesNotContain("Nova", updated.Filter.Assignees);
        Assert.Contains("@unassigned", updated.Filter.Assignees);
    }
}
