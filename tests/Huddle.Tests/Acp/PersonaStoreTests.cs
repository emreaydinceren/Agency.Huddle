using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Acp;

public sealed class PersonaStoreTests
{
    [Fact]
    public void Add_WritesMarkdownFile()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        store.Add("coo", "You are the Chief of Staff.");

        var path = Path.Combine(dir.Path, "personas", "coo.md");
        Assert.True(File.Exists(path));
        Assert.Equal("You are the Chief of Staff.", File.ReadAllText(path));
    }

    [Fact]
    public void Add_AcceptsNameWithSpaces_AndRoundTrips()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        store.Add("Chief of Staff", "You keep the team honest.");

        var path = Path.Combine(dir.Path, "personas", "Chief of Staff.md");
        Assert.True(File.Exists(path));

        // Listing reads the Name back off the filesystem, so this is what proves the spaces
        // survive the round trip rather than being mangled or trimmed by the file layer.
        Assert.Contains("Chief of Staff", store.ListNames());

        var persona = store.Get("Chief of Staff");
        Assert.NotNull(persona);
        Assert.Equal("Chief of Staff", persona.Name);
    }

    [Fact]
    public void Add_RejectsNameThatWouldEscapeThePersonaDirectory()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        // Allowing spaces must not have loosened the path-traversal guard.
        Assert.Throws<ChatException>(() => store.Add("../escape", "x"));
        Assert.Throws<ChatException>(() => store.Add("sub/coo", "x"));
        Assert.Throws<ChatException>(() => store.Add("trailing ", "x"));
    }

    [Fact]
    public void Get_ReturnsFileText()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add("coo", "You are the Chief of Staff.");

        var persona = store.Get("coo");

        Assert.NotNull(persona);
        Assert.Equal("coo", persona.Name);
        Assert.Equal("You are the Chief of Staff.", persona.Text);
    }

    [Fact]
    public void Get_ReturnsNullWhenMissing()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        var persona = store.Get("nope");

        Assert.Null(persona);
    }

    [Fact]
    public void Get_JoinsTheStoredModelToTheFile()
    {
        // Proves the join at its one site: the .md file and the model row are written through
        // entirely separate paths (a hand-written file, a direct PersonaModelStore.Set), yet
        // PersonaStore.Get comes back with both.
        using var dir = new TempDataDir();
        var options = dir.Options();
        var personaDir = Path.Combine(dir.Path, "personas");
        Directory.CreateDirectory(personaDir);
        File.WriteAllText(Path.Combine(personaDir, "coo.md"), "You are the Chief of Staff.");
        var models = new PersonaModelStore(options);
        models.Set("coo", "claude-opus-4");
        using var store = new PersonaStore(options, models, new PersonaEffortStore(options));

        var persona = store.Get("coo");

        Assert.NotNull(persona);
        Assert.Equal("You are the Chief of Staff.", persona.Text);
        Assert.Equal("claude-opus-4", persona.Model);
    }

    [Fact]
    public void Get_JoinsTheStoredEffortToTheFile()
    {
        // Mirrors Get_JoinsTheStoredModelToTheFile: the .md file and the effort row are written
        // through entirely separate paths (a hand-written file, a direct PersonaEffortStore.Set),
        // yet PersonaStore.Get comes back with both.
        using var dir = new TempDataDir();
        var options = dir.Options();
        var personaDir = Path.Combine(dir.Path, "personas");
        Directory.CreateDirectory(personaDir);
        File.WriteAllText(Path.Combine(personaDir, "coo.md"), "You are the Chief of Staff.");
        var efforts = new PersonaEffortStore(options);
        efforts.Set("coo", "high");
        using var store = new PersonaStore(options, new PersonaModelStore(options), efforts);

        var persona = store.Get("coo");

        Assert.NotNull(persona);
        Assert.Equal("You are the Chief of Staff.", persona.Text);
        Assert.Equal("high", persona.Effort);
    }

    [Fact]
    public void ListNames_ReturnsNamesWithoutExtension()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add("beta", "text beta");
        store.Add("alpha", "text alpha");

        var names = store.ListNames();

        Assert.Equal(["alpha", "beta"], names);
    }

    [Fact]
    public void ListNames_EmptyWhenDirectoryMissing()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        var names = store.ListNames();

        Assert.Empty(names);
    }

    [Fact]
    public void Add_RejectsInvalidName()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        var ex = Assert.Throws<ChatException>(() => store.Add("../evil", "text"));

        Assert.Equal(ErrorCodes.BadMessage, ex.Code);
        Assert.False(File.Exists(Path.Combine(dir.Path, "personas", "evil.md")));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(dir.Path)!, "evil.md")));
    }

    [Fact]
    public void Add_RejectsDuplicate()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add("coo", "first");

        var ex = Assert.Throws<ChatException>(() => store.Add("coo", "second"));

        Assert.Equal(ErrorCodes.BadMessage, ex.Code);
    }

    [Fact]
    public void Add_RejectsBlankText()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        var ex = Assert.Throws<ChatException>(() => store.Add("coo", "   "));

        Assert.Equal(ErrorCodes.BadMessage, ex.Code);
    }

    [Fact]
    public void Add_WithAModel_StoresIt()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        var persona = store.Add("coo", "text", "claude-opus-4");

        Assert.Equal("claude-opus-4", persona.Model);
        Assert.Equal("claude-opus-4", store.Get("coo")!.Model);
    }

    [Fact]
    public void Add_WithoutAModel_LeavesItUnset()
    {
        // The agent-default case: a new Persona has no model until one is explicitly chosen.
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        var persona = store.Add("coo", "text");

        Assert.Null(persona.Model);
        Assert.Null(store.Get("coo")!.Model);
    }

    [Fact]
    public void Add_WithAnEffort_StoresIt()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        var persona = store.Add("coo", "text", "claude-opus-4", "high");

        Assert.Equal("high", persona.Effort);
        Assert.Equal("high", store.Get("coo")!.Effort);
    }

    [Fact]
    public void Add_WithoutAnEffort_LeavesItUnset()
    {
        // The model-default case: a new Persona has no effort until one is explicitly chosen.
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        var persona = store.Add("coo", "text");

        Assert.Null(persona.Effort);
        Assert.Null(store.Get("coo")!.Effort);
    }

    [Fact]
    public void Add_RaisesPersonasChanged()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        var raised = false;
        store.PersonasChanged += () => raised = true;

        store.Add("coo", "text");

        Assert.True(raised);
    }

    [Fact]
    public void Update_OverwritesTheFile()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add("coo", "first");

        var updated = store.Update("coo", "second", model: null, effort: null);

        Assert.Equal("second", updated.Text);
        var path = Path.Combine(dir.Path, "personas", "coo.md");
        Assert.Equal("second", File.ReadAllText(path));
    }

    [Fact]
    public void Update_UnknownPersona_Throws()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        var ex = Assert.Throws<ChatException>(() => store.Update("nope", "text", model: null, effort: null));

        Assert.Equal(ErrorCodes.BadMessage, ex.Code);
    }

    [Fact]
    public void Update_RejectsBlankText()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add("coo", "first");

        var ex = Assert.Throws<ChatException>(() => store.Update("coo", "   ", model: null, effort: null));

        Assert.Equal(ErrorCodes.BadMessage, ex.Code);
        Assert.Equal("first", store.Get("coo")!.Text);
    }

    [Fact]
    public void Update_RaisesPersonasChanged()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add("coo", "first");
        var raised = false;
        store.PersonasChanged += () => raised = true;

        store.Update("coo", "second", model: null, effort: null);

        Assert.True(raised);
    }

    [Fact]
    public void Update_ChangingOnlyTheModel_KeepsTheText()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add("coo", "unchanged text");

        var updated = store.Update("coo", "unchanged text", "claude-opus-4", effort: null);

        Assert.Equal("unchanged text", updated.Text);
        Assert.Equal("claude-opus-4", updated.Model);
        var path = Path.Combine(dir.Path, "personas", "coo.md");
        Assert.Equal("unchanged text", File.ReadAllText(path));
    }

    [Fact]
    public void Update_ChangingOnlyTheEffort_KeepsTheTextAndTheModel()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add("coo", "unchanged text", "claude-opus-4");

        var updated = store.Update("coo", "unchanged text", "claude-opus-4", "high");

        Assert.Equal("unchanged text", updated.Text);
        Assert.Equal("claude-opus-4", updated.Model);
        Assert.Equal("high", updated.Effort);
        var path = Path.Combine(dir.Path, "personas", "coo.md");
        Assert.Equal("unchanged text", File.ReadAllText(path));
    }

    [Fact]
    public void Update_RaisesPersonasChangedOnce()
    {
        // The three-restarts/three-"node"-spawns guard: writing the file, the model and the effort
        // must not each raise their own PersonasChanged, or PersonaSupervisor would restart the
        // runner three times for a single save.
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add("coo", "first");
        var raisedCount = 0;
        store.PersonasChanged += () => raisedCount++;

        store.Update("coo", "second", "claude-opus-4", "high");

        Assert.Equal(1, raisedCount);
    }

    [Fact]
    public void Remove_DeletesTheFile()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add("coo", "text");
        var path = Path.Combine(dir.Path, "personas", "coo.md");

        store.Remove("coo");

        Assert.False(File.Exists(path));
        Assert.Null(store.Get("coo"));
    }

    [Fact]
    public void Remove_AlsoRemovesTheStoredModel()
    {
        // A stale model must not resurrect: if it did, re-creating a Persona of the same name would
        // silently inherit a model nobody chose for it this time.
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add("coo", "text", "claude-opus-4");

        store.Remove("coo");
        var recreated = store.Add("coo", "new text");

        Assert.Null(recreated.Model);
    }

    [Fact]
    public void Remove_AlsoRemovesTheStoredEffort()
    {
        // A stale effort must not resurrect: if it did, re-creating a Persona of the same name
        // would silently inherit an effort nobody chose for it this time.
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add("coo", "text", "claude-opus-4", "high");

        store.Remove("coo");
        var recreated = store.Add("coo", "new text");

        Assert.Null(recreated.Effort);
    }

    [Fact]
    public void Remove_UnknownPersona_Throws()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        var ex = Assert.Throws<ChatException>(() => store.Remove("nope"));

        Assert.Equal(ErrorCodes.BadMessage, ex.Code);
    }

    [Fact]
    public void Remove_RaisesPersonasChanged()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add("coo", "text");
        var raised = false;
        store.PersonasChanged += () => raised = true;

        store.Remove("coo");

        Assert.True(raised);
    }

    [Fact]
    public async Task Remove_LeavesTheAgentAndItsRoomsIntact()
    {
        // The surprising-but-intended behaviour: PersonaStore has no reference to ITeamDirectory at
        // all, so Remove() cannot cascade. This proves it through the real Team Directory: an Agent and a
        // Room created for the Persona survive Remove() untouched.
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add("coo", "text");

        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var room = await directory.CreateRoomAsync("coo", [KnownIds.Human, agent.Id], ct);

        store.Remove("coo");

        var stillThere = await directory.FindUserByNameAsync("coo", ct);
        Assert.NotNull(stillThere);
        var stillRoom = await directory.GetRoomAsync(room.Id, ct);
        Assert.NotNull(stillRoom);
    }

    [Fact]
    public void PathFor_RejectsTraversalNames()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        var ex = Assert.Throws<ChatException>(() => store.PathFor("../evil"));

        Assert.Equal(ErrorCodes.BadMessage, ex.Code);
    }

    [Fact]
    public void PathFor_ReturnsAbsolutePathUnderThePersonaDirectory()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add("coo", "text");

        var path = store.PathFor("coo");

        Assert.Equal(Path.Combine(dir.Path, "personas", "coo.md"), path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task ExternalFileChange_IsNoticedThroughPersonasChanged()
    {
        // Proves the FileSystemWatcher (not just calls through PersonaStore itself) drives
        // PersonasChanged: this writes a .md file directly to disk, bypassing the store entirely.
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        Directory.CreateDirectory(Path.Combine(dir.Path, "personas"));

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.PersonasChanged += () => tcs.TrySetResult();

        await File.WriteAllTextAsync(Path.Combine(dir.Path, "personas", "external.md"), "You are External.", TestContext.Current.CancellationToken);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var registration = cts.Token.Register(() => tcs.TrySetCanceled());
        await tcs.Task;

        Assert.Contains("external", store.ListNames());
    }

    private static PersonaStore CreateStore(TempDataDir dir)
    {
        return new PersonaStore(dir.Options(), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()));
    }
}