using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.App.Watching;
using Agency.Huddle.Tests.Elicitation;

namespace Agency.Huddle.Tests.Tasks.Views;

/// <summary>
/// Tests for <see cref="ViewStore"/>: Spec §12.3's four deliberate differences from
/// <see cref="Agency.Huddle.App.Avatars.AvatarStore"/> (a malformed file sets <c>LoadError</c> and
/// refuses every write rather than falling back to empty, writes go through a temp file plus an
/// atomic move, an invalid View entry is kept and flagged rather than skipped, and the two built-in
/// Views are created in memory and only written once edited), and Spec §9.6's
/// <see cref="ViewStore.RenameAgent(string, string)"/>.
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

    /// <summary>The built-in All Tasks and My Tasks Views, and a View created from the sidebar, start with id, title, status, priority, assignee and due date (#721).</summary>
    [Fact]
    public void BuiltInsAndANewView_StartWithTheDefaultListFields()
    {
        string[] expected = ["id", "title", "status", "priority", "assignee", "due_date"];
        using TempDataDir dataDir = new();
        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);

        Assert.Equal(expected, Assert.Single(store.Views, v => v.Id == "all-tasks").Fields);
        Assert.Equal(expected, Assert.Single(store.Views, v => v.Id == "my-tasks").Fields);
        TaskView created = Agency.Huddle.App.Components.Tasks.ViewActions.CreateNew(store) ?? throw new InvalidOperationException("The View was not saved.");
        Assert.Equal(expected, created.Fields);
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

        ViewLoadError? loadError = store.LoadError;
        Assert.NotNull(loadError);
        Assert.Equal((expected.LineNumber ?? 0) + 1, loadError.Line);
        Assert.Equal((expected.BytePositionInLine ?? 0) + 1, loadError.Column);

        ViewSaveResult result = store.Save(ListView("custom-1", "Custom View"));

        Assert.False(result.Saved);
        Assert.Equal([$"'{path}' has a parse error ('{path}' could not be parsed: {expected.Message}) and must be fixed by hand before a View can be saved."], result.Problems);
        Assert.Equal(malformed, File.ReadAllText(path));
    }

    /// <summary>
    /// A file that parses cleanly at startup but is raced to malformed JSON between
    /// <see cref="ViewStore.Save(TaskView)"/>'s in-memory check and its re-read from disk still
    /// refuses the save, with the same parse-error problem text as the startup case.
    /// </summary>
    [Fact]
    public void Save_ConcurrentlyMalformedFile_Refused()
    {
        using TempDataDir dataDir = new();
        string path = Path.Combine(dataDir.Path, "views.json");
        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);
        Assert.Null(store.LoadError);

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

        ViewSaveResult result = store.Save(ListView("custom-1", "Custom View"));

        Assert.False(result.Saved);
        Assert.Equal([$"'{path}' has a parse error ('{path}' could not be parsed: {expected.Message}) and must be fixed by hand before a View can be saved."], result.Problems);
    }

    /// <summary>
    /// A file that parses cleanly at startup but is raced to malformed JSON between
    /// <see cref="ViewStore.Delete(string)"/>'s in-memory check and its re-read from disk still
    /// refuses the delete, with the same parse-error problem text as the startup case.
    /// </summary>
    [Fact]
    public void Delete_ConcurrentlyMalformedFile_Refused()
    {
        using TempDataDir dataDir = new();
        string path = Path.Combine(dataDir.Path, "views.json");
        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);
        store.Save(ListView("custom-1", "Custom View"));
        Assert.Null(store.LoadError);

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

        ViewSaveResult result = store.Delete("custom-1");

        Assert.False(result.Saved);
        Assert.Equal([$"'{path}' has a parse error ('{path}' could not be parsed: {expected.Message}) and must be fixed by hand before a View can be deleted."], result.Problems);
    }

    /// <summary>A malformed file at startup also refuses a delete, with the delete-specific parse-error problem text.</summary>
    [Fact]
    public void Delete_MalformedFile_Refused()
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

        ViewSaveResult result = store.Delete("custom-1");

        Assert.False(result.Saved);
        Assert.Equal([$"'{path}' has a parse error ('{path}' could not be parsed: {expected.Message}) and must be fixed by hand before a View can be deleted."], result.Problems);
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
        string path = Path.Combine(dataDir.Path, "views.json");
        File.WriteAllText(path, string.Empty);

        JsonException? expected = null;
        try
        {
            JsonNode.Parse(string.Empty);
        }
        catch (JsonException ex)
        {
            expected = ex;
        }

        Assert.NotNull(expected);

        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);

        ViewLoadError? loadError = store.LoadError;
        Assert.NotNull(loadError);
        Assert.Equal(1, loadError.Line);
        Assert.Equal(1, loadError.Column);
        Assert.Equal($"'{path}' could not be parsed: {expected.Message}", loadError.Message);
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
        Assert.NotNull(node);
        JsonNode? viewsNode = node["views"];
        Assert.NotNull(viewsNode);
        JsonArray views = viewsNode.AsArray();
        JsonObject badEntry = views
            .OfType<JsonObject>()
            .Single(entry => string.Equals(entry["id"]?.GetValue<string>(), "bad-1", StringComparison.Ordinal));
        Assert.Equal("nonsense", badEntry["kind"]?.GetValue<string>());
        Assert.Equal("keepme", badEntry["customField"]?.GetValue<string>());
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

        ViewSaveResult result = store.Save(allTasks with { Name = "Everything" });

        Assert.True(result.Saved);
        Assert.True(File.Exists(path));
        JsonNode? node = JsonNode.Parse(File.ReadAllText(path));
        Assert.NotNull(node);
        JsonNode? viewsNode = node["views"];
        Assert.NotNull(viewsNode);
        JsonArray writtenViews = viewsNode.AsArray();
        JsonObject writtenView = Assert.Single(writtenViews.OfType<JsonObject>(), v => string.Equals(v["id"]?.GetValue<string>(), "all-tasks", StringComparison.Ordinal));
        Assert.Equal("Everything", writtenView["name"]?.GetValue<string>());
        TaskView? updated = store.Get("all-tasks");
        Assert.NotNull(updated);
        Assert.Equal("Everything", updated.Name);
    }

    /// <summary>Deleting a built-in View is refused, and it is still there afterward.</summary>
    [Fact]
    public void Delete_BuiltIn_Refused()
    {
        using TempDataDir dataDir = new();
        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);

        ViewSaveResult result = store.Delete("all-tasks");

        Assert.False(result.Saved);
        Assert.Equal(["A built-in View cannot be deleted."], result.Problems);
        Assert.Contains(store.Views, v => v.Id == "all-tasks");
    }

    /// <summary>
    /// The watcher's re-read of the store's own write changes nothing, so it raises no second
    /// <see cref="ViewStore.ViewsChanged"/>. A subscribed component used to re-render about half a second after
    /// every save, retiring the event-handler ids of elements it had already rendered.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task Save_WatcherRereadOfOwnWrite_RaisesNoSecondViewsChanged()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);
        int changedCount = 0;
        store.ViewsChanged += () => Interlocked.Increment(ref changedCount);

        Assert.True(store.Save(ListView("custom-1", "Custom View")).Saved);
        await Task.Delay(TimeSpan.FromMilliseconds(DebouncedFileWatcher.DebounceMilliseconds * 3), ct);

        Assert.Equal(1, Volatile.Read(ref changedCount));
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

        ViewSaveResult second = store.Save(ListView("custom-1", "Sprint Board") with { Fields = ["status"] });

        Assert.True(second.Saved);
        Assert.Empty(second.Problems);
        TaskView? renamed = store.Get("custom-1");
        Assert.NotNull(renamed);
        Assert.Equal(["status"], renamed.Fields);
    }

    /// <summary><see cref="ViewStore.RenameAgent(string, string)"/> rewrites every View's assignee filter that named the old Agent, leaving other assignee values untouched.</summary>
    [Fact]
    public void RenameAgent_RewritesAssigneeFilters()
    {
        using TempDataDir dataDir = new();
        using ViewStore store = new(dataDir.Options(), NullLogger<ViewStore>.Instance);
        store.Save(ListView("custom-1", "Custom View", assignees: ["Nova", "@unassigned"]));

        store.RenameAgent("Nova", "NovaPrime");

        TaskView? updated = store.Get("custom-1");
        Assert.NotNull(updated);
        Assert.Contains("NovaPrime", updated.Filter.Assignees);
        Assert.DoesNotContain("Nova", updated.Filter.Assignees);
        Assert.Contains("@unassigned", updated.Filter.Assignees);
    }

    /// <summary>
    /// A watcher rebuild that cannot read <c>views.json</c> on any attempt (an editor holds it exclusively) logs a
    /// warning and keeps the previously resolved Views, with no <see cref="ViewStore.LoadError"/>, instead of letting the <see cref="IOException"/> escape the timer thread.
    /// </summary>
    [Fact]
    public async Task ExternalEdit_FileLockedForEveryRetry_KeepsThePreviousViews()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        string path = Path.Combine(dataDir.Path, "views.json");
        File.WriteAllText(path, """{ "version": 1, "views": [ { "id": "before-1", "name": "Before", "kind": "list" } ] }""");
        RecordingLogger<ViewStore> logger = new();
        using ViewStore store = new(dataDir.Options(), logger);
        Assert.Contains(store.Views, v => v.Id == "before-1");

        using (FileStream held = LockedWatcherFileSupport.WriteAndHold(path, """{ "version": 1, "views": [ { "id": "after-1", "name": "After", "kind": "list" } ] }"""))
        {
            await LockedWatcherFileSupport.WaitForWarningAsync(logger, ct);

            Assert.Contains(store.Views, v => v.Id == "before-1");
            Assert.DoesNotContain(store.Views, v => v.Id == "after-1");
            Assert.Null(store.LoadError);
        }
    }
}
