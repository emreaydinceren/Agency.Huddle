using System.Runtime.Versioning;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Elicitation;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Acp;

public sealed class PersonaStoreTests
{
    [Fact]
    public void Add_WritesMarkdownFile()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        store.Add(TestPersonas.Identity("coo"), "You are the Chief of Staff.");

        var path = store.PathFor("coo");
        Assert.True(File.Exists(path));

        // Add composes its own canonical front matter - lowercase keys, single-quoted scalars -
        // rather than writing whatever raw text a caller handed it. That composition (and its
        // apostrophe-escaping and Teams-as-flow-list behaviour) is Add_ComposesFrontMatter...'s job
        // below and PersonaFrontmatterTests' Compose_* facts; this is just the plain happy path.
        Assert.Equal(
            "---\nname: 'coo'\ntitle: 'coo'\nalias: 'coo'\n---\nYou are the Chief of Staff.",
            File.ReadAllText(path));
    }

    [Fact]
    public void Add_AcceptsNameWithSpaces_AndRoundTrips()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        store.Add(TestPersonas.Identity("Chief of Staff"), "You keep the team honest.");

        var path = store.PathFor("Chief of Staff");
        Assert.True(File.Exists(path));

        // Listing reads the Name back off the composed frontmatter, so this is what proves the
        // spaces survive the round trip rather than being mangled or trimmed anywhere along the way.
        Assert.Contains("Chief of Staff", store.ListNames());

        var persona = store.Get("Chief of Staff");
        Assert.NotNull(persona);
        Assert.Equal("Chief of Staff", persona.Name);
    }

    /// <summary>
    /// A file written by <see cref="PersonaStore.Add"/> must load cleanly back through the whole
    /// stack it was written for - including a Title containing an apostrophe, and a Teams list -
    /// proving the frontmatter Add composes is not just plausible-looking text but something its
    /// own reader accepts.
    /// </summary>
    [Fact]
    public void Add_ComposesFrontMatterThatRoundTripsThroughPersonaIndex_EvenWithAnApostropheInTheTitle()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        var identity = new PersonaIdentity("coo", "The Boss's Assistant", "coo", ["Business", "Household"]);

        store.Add(identity, "You are the Chief of Staff.");

        var path = store.PathFor("coo");
        var written = File.ReadAllText(path);

        // YAML's own escape for an embedded apostrophe inside a single-quoted scalar is "''" - the
        // file on disk must carry the ESCAPED form.
        Assert.Contains("title: 'The Boss''s Assistant'", written, StringComparison.Ordinal);
        Assert.Contains("teams: ['Business', 'Household']", written, StringComparison.Ordinal);

        // ...and it must load back with the apostrophe UNescaped again - the actual round trip
        // this test is named for.
        var entry = Assert.Single(store.Entries);
        Assert.Equal("coo", entry.Name);
        Assert.Equal("The Boss's Assistant", entry.Title);
        Assert.Equal(["Business", "Household"], entry.Teams);

        var persona = store.Get("coo");
        Assert.NotNull(persona);
        Assert.EndsWith("You are the Chief of Staff.", persona.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Add_RejectsNameThatWouldEscapeThePersonaDirectory()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        // Allowing spaces must not have loosened the path-traversal guard. This is still the one
        // place NameRules.IsValidAgentName has to run against the Name before it is concatenated
        // into a path - every other method now resolves a path through the index instead.
        Assert.Throws<ChatException>(() => store.Add(TestPersonas.Identity("../escape"), "x"));
        Assert.Throws<ChatException>(() => store.Add(TestPersonas.Identity("sub/coo"), "x"));
        Assert.Throws<ChatException>(() => store.Add(TestPersonas.Identity("trailing "), "x"));
    }

    /// <summary>An Alias never reaches a path, but Add guards it with the same NameRules check as Name - both are identifiers a caller could otherwise write something invalid into.</summary>
    [Fact]
    public void Add_RejectsInvalidAlias()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        var ex = Assert.Throws<ChatException>(() => store.Add(TestPersonas.Identity("coo", alias: "bad/alias"), "text"));

        Assert.Equal(ErrorCodes.BadMessage, ex.Code);
        Assert.False(File.Exists(Path.Combine(dir.Path, "Agents", "coo.md")));
    }

    [Fact]
    public void Get_ReturnsFileText()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(TestPersonas.Identity("coo"), "You are the Chief of Staff.");

        var persona = store.Get("coo");

        Assert.NotNull(persona);
        Assert.Equal("coo", persona.Name);
        Assert.Equal(
            "---\nname: 'coo'\ntitle: 'coo'\nalias: 'coo'\n---\nYou are the Chief of Staff.",
            persona.Text);
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
        TestPersonaFiles.Write(new AgentPaths(options), "coo", TestPersonas.FrontmatterText("coo", "You are the Chief of Staff."));
        var models = new PersonaModelStore(options);
        models.Set("coo", "claude-opus-4");
        using var store = new PersonaStore(new AgentPaths(options), models, new PersonaEffortStore(options), new PersonaWorkModeStore(options), NullLogger<PersonaStore>.Instance);

        var persona = store.Get("coo");

        Assert.NotNull(persona);
        Assert.Equal(TestPersonas.FrontmatterText("coo", "You are the Chief of Staff."), persona.Text);
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
        TestPersonaFiles.Write(new AgentPaths(options), "coo", TestPersonas.FrontmatterText("coo", "You are the Chief of Staff."));
        var efforts = new PersonaEffortStore(options);
        efforts.Set("coo", "high");
        using var store = new PersonaStore(new AgentPaths(options), new PersonaModelStore(options), efforts, new PersonaWorkModeStore(options), NullLogger<PersonaStore>.Instance);

        var persona = store.Get("coo");

        Assert.NotNull(persona);
        Assert.Equal(TestPersonas.FrontmatterText("coo", "You are the Chief of Staff."), persona.Text);
        Assert.Equal("high", persona.Effort);
    }

    /// <summary>A file whose frontmatter carries an <c>adapter:</c> value round-trips it through <see cref="PersonaStore.Get(string)"/> onto <see cref="Persona.Adapter"/> - the file-to-database join <see cref="PersonaStore.Get(string)"/> performs (Spec §7.3).</summary>
    [Fact]
    public void Get_ReturnsTheAdapterFromFrontmatter()
    {
        using var dir = new TempDataDir();
        var options = dir.Options();
        TestPersonaFiles.Write(new AgentPaths(options), "coo", TestPersonas.FrontmatterText("coo", "You are the Chief of Staff.", adapter: "agency"));
        using var store = options.CreatePersonaStore();

        var persona = store.Get("coo");

        Assert.NotNull(persona);
        Assert.Equal("agency", persona.Adapter);
    }

    /// <summary>A file with no <c>adapter:</c> field is still a valid Persona (Spec §7.2), and its Adapter is <see langword="null"/> - the installation's default profile.</summary>
    [Fact]
    public void Get_WithNoAdapterInFrontmatter_ReturnsNullAdapter()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(TestPersonas.Identity("coo"), "You are the Chief of Staff.");

        var persona = store.Get("coo");

        Assert.NotNull(persona);
        Assert.Null(persona.Adapter);
    }

    /// <summary>
    /// <see cref="PersonaStore.Update"/> writes raw text through unchanged - Spec §7.2's "WriteScalarField path" note - so a
    /// hand-written <c>adapter:</c> line survives an Update that does not touch it, the same guarantee the rest of the
    /// frontmatter already gets.
    /// </summary>
    [Fact]
    public void Update_WithUnchangedText_PreservesAHandWrittenAdapterLine()
    {
        using var dir = new TempDataDir();
        var options = dir.Options();
        var text = TestPersonas.FrontmatterText("coo", "first", adapter: "agency");
        TestPersonaFiles.Write(new AgentPaths(options), "coo", text);
        using var store = options.CreatePersonaStore();

        var updated = store.Update("coo", text, model: null, effort: null, workMode: null);

        Assert.Equal("agency", updated.Adapter);
        Assert.Equal("agency", store.Get("coo")!.Adapter);
    }

    [Fact]
    public void ListNames_ReturnsNamesWithoutExtension()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(TestPersonas.Identity("beta"), "text beta");
        store.Add(TestPersonas.Identity("alpha"), "text alpha");

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

        var ex = Assert.Throws<ChatException>(() => store.Add(TestPersonas.Identity("../evil"), "text"));

        Assert.Equal(ErrorCodes.BadMessage, ex.Code);
        Assert.False(File.Exists(Path.Combine(dir.Path, "Agents", "evil.md")));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(dir.Path)!, "evil.md")));
    }

    [Fact]
    public void Add_RejectsDuplicate()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(TestPersonas.Identity("coo"), "first");

        // Fails on the physical file already existing at "coo.md" - a check that runs before, and
        // independently of, whether "second" would itself load cleanly.
        var ex = Assert.Throws<ChatException>(() => store.Add(TestPersonas.Identity("coo"), "second"));

        Assert.Equal(ErrorCodes.BadMessage, ex.Code);
    }

    [Fact]
    public void Add_RejectsBlankText()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        var ex = Assert.Throws<ChatException>(() => store.Add(TestPersonas.Identity("coo"), "   "));

        Assert.Equal(ErrorCodes.BadMessage, ex.Code);
    }

    /// <summary>A blank Title cannot load, so it must be rejected up front rather than written and then silently invisible - the one required field Add itself does not pre-validate before composing, relying on ValidateCandidate instead.</summary>
    [Fact]
    public void Add_RejectsBlankTitle()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        var ex = Assert.Throws<ChatException>(() => store.Add(TestPersonas.Identity("coo", title: "   "), "text"));

        Assert.Equal(ErrorCodes.BadMessage, ex.Code);
        Assert.False(File.Exists(Path.Combine(dir.Path, "Agents", "coo.md")));
    }

    [Fact]
    public void Add_WithAModel_StoresIt()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        var persona = store.Add(TestPersonas.Identity("coo"), "text", "claude-opus-4");

        Assert.Equal("claude-opus-4", persona.Model);
        Assert.Equal("claude-opus-4", store.Get("coo")!.Model);
    }

    [Fact]
    public void Add_WithoutAModel_LeavesItUnset()
    {
        // The agent-default case: a new Persona has no model until one is explicitly chosen.
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        var persona = store.Add(TestPersonas.Identity("coo"), "text");

        Assert.Null(persona.Model);
        Assert.Null(store.Get("coo")!.Model);
    }

    [Fact]
    public void Add_WithAnEffort_StoresIt()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        var persona = store.Add(TestPersonas.Identity("coo"), "text", "claude-opus-4", "high");

        Assert.Equal("high", persona.Effort);
        Assert.Equal("high", store.Get("coo")!.Effort);
    }

    [Fact]
    public void Add_WithoutAnEffort_LeavesItUnset()
    {
        // The model-default case: a new Persona has no effort until one is explicitly chosen.
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        var persona = store.Add(TestPersonas.Identity("coo"), "text");

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

        store.Add(TestPersonas.Identity("coo"), "text");

        Assert.True(raised);
    }

    [Fact]
    public void Update_OverwritesTheFile()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(TestPersonas.Identity("coo"), "first");

        var updated = store.Update("coo", TestPersonas.FrontmatterText("coo", "second"), model: null, effort: null, workMode: null);

        Assert.Equal(TestPersonas.FrontmatterText("coo", "second"), updated.Text);
        var path = store.PathFor("coo");
        Assert.Equal(TestPersonas.FrontmatterText("coo", "second"), File.ReadAllText(path));
    }

    [Fact]
    public void Update_UnknownPersona_Throws()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        var ex = Assert.Throws<ChatException>(() => store.Update("nope", "text", model: null, effort: null, workMode: null));

        Assert.Equal(ErrorCodes.BadMessage, ex.Code);
    }

    [Fact]
    public void Update_RejectsBlankText()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        var added = store.Add(TestPersonas.Identity("coo"), "first");

        var ex = Assert.Throws<ChatException>(() => store.Update("coo", "   ", model: null, effort: null, workMode: null));

        Assert.Equal(ErrorCodes.BadMessage, ex.Code);

        // Add composes its own frontmatter (lowercase keys, quoted scalars) rather than the
        // capitalised, unquoted style PersonaText hand-writes, so the file left behind by a
        // rejected Update is compared against what Add itself actually wrote, not PersonaText.
        Assert.Equal(added.Text, store.Get("coo")!.Text);
    }

    /// <summary>A save that would not load cleanly is rejected before it is written, and the file on disk must survive byte-for-byte.</summary>
    [Fact]
    public async Task Update_WithTextThatWouldBeRejected_ThrowsAndLeavesTheFileByteIdentical()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(TestPersonas.Identity("coo"), "You are the Chief of Staff.");
        var path = store.PathFor("coo");
        var before = await File.ReadAllBytesAsync(path, ct);

        var ex = Assert.Throws<ChatException>(() => store.Update("coo", "You are the Chief of Staff, but with no frontmatter at all now.", model: null, effort: null, workMode: null));

        Assert.Equal(ErrorCodes.BadMessage, ex.Code);
        var after = await File.ReadAllBytesAsync(path, ct);
        Assert.Equal(before, after);
    }

    [Fact]
    public void Update_RaisesPersonasChanged()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(TestPersonas.Identity("coo"), "first");
        var raised = false;
        store.PersonasChanged += () => raised = true;

        store.Update("coo", TestPersonas.FrontmatterText("coo", "second"), model: null, effort: null, workMode: null);

        Assert.True(raised);
    }

    [Fact]
    public void Update_ChangingOnlyTheModel_KeepsTheText()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(TestPersonas.Identity("coo"), "unchanged text");

        var updated = store.Update("coo", TestPersonas.FrontmatterText("coo", "unchanged text"), "claude-opus-4", effort: null, workMode: null);

        Assert.Equal(TestPersonas.FrontmatterText("coo", "unchanged text"), updated.Text);
        Assert.Equal("claude-opus-4", updated.Model);
        var path = store.PathFor("coo");
        Assert.Equal(TestPersonas.FrontmatterText("coo", "unchanged text"), File.ReadAllText(path));
    }

    [Fact]
    public void Update_ChangingOnlyTheEffort_KeepsTheTextAndTheModel()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(TestPersonas.Identity("coo"), "unchanged text", "claude-opus-4");

        var updated = store.Update("coo", TestPersonas.FrontmatterText("coo", "unchanged text"), "claude-opus-4", "high", workMode: null);

        Assert.Equal(TestPersonas.FrontmatterText("coo", "unchanged text"), updated.Text);
        Assert.Equal("claude-opus-4", updated.Model);
        Assert.Equal("high", updated.Effort);
        var path = store.PathFor("coo");
        Assert.Equal(TestPersonas.FrontmatterText("coo", "unchanged text"), File.ReadAllText(path));
    }

    [Fact]
    public void Update_RaisesPersonasChangedOnce()
    {
        // The three-restarts/three-"node"-spawns guard: writing the file, the model and the effort
        // must not each raise their own PersonasChanged, or PersonaSupervisor would restart the
        // runner three times for a single save.
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(TestPersonas.Identity("coo"), "first");
        var raisedCount = 0;
        void Count() => Interlocked.Increment(ref raisedCount);
        store.PersonasChanged += Count;

        store.Update("coo", TestPersonas.FrontmatterText("coo", "second"), "claude-opus-4", "high", workMode: null);

        // Stop counting the instant Update returns. This is what the test is about - the
        // SYNCHRONOUS raise - and without it the assertion is a race the test loses on a slow
        // machine: Update writes the file, the FileSystemWatcher sees that write like any other,
        // and raises a SECOND PersonasChanged once its 500 ms debounce elapses. That later event
        // is expected and harmless (PersonaSupervisor.NeedsRestart compares the Persona by value,
        // so a re-read of an unchanged file restarts nothing), but it is not this guard's subject.
        // Left subscribed, this passed locally and failed on CI, where the container is slow
        // enough for the debounce to land before the assertion.
        store.PersonasChanged -= Count;

        Assert.Equal(1, raisedCount);
    }

    /// <summary>Editing <c>name:</c> through <see cref="PersonaStore.Update"/> announces the rename exactly once, carrying both the old and the new Name.</summary>
    [Fact]
    public void Update_ChangingTheName_RaisesPersonaRenamedOnceWithTheOldAndTheNewName()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(TestPersonas.Identity("coo"), "first");
        var renames = new List<PersonaRenamed>();
        store.PersonaRenamed += renamed => renames.Add(renamed);

        store.Update("coo", TestPersonas.FrontmatterText("vp", "second"), model: null, effort: null, workMode: null);

        var renamed = Assert.Single(renames);
        Assert.Equal("coo", renamed.OldName);
        Assert.Equal("vp", renamed.NewName);
    }

    /// <summary>
    /// The ordering guarantee the cascade (a future PersonaSupervisor/Team Directory rename) depends
    /// on: PersonaSupervisor reacts to PersonasChanged by starting the new Name's runner, which
    /// registers over the pipe immediately - so PersonaRenamed must be raised, and fully handled, BEFORE
    /// PersonasChanged, never after or interleaved.
    /// </summary>
    [Fact]
    public void Update_ChangingTheName_RaisesPersonaRenamedBeforePersonasChanged()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(TestPersonas.Identity("coo"), "first");
        var order = new List<string>();
        store.PersonaRenamed += _ => order.Add("PersonaRenamed");
        store.PersonasChanged += () => order.Add("PersonasChanged");

        store.Update("coo", TestPersonas.FrontmatterText("vp", "second"), model: null, effort: null, workMode: null);

        Assert.Equal(["PersonaRenamed", "PersonasChanged"], order);
    }

    /// <summary>Editing the body without touching <c>name:</c> is an ordinary edit, not a rename.</summary>
    [Fact]
    public void Update_ChangingOnlyTheText_RaisesNoPersonaRenamed()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(TestPersonas.Identity("coo"), "first");
        var raised = false;
        store.PersonaRenamed += _ => raised = true;

        store.Update("coo", TestPersonas.FrontmatterText("coo", "second"), model: null, effort: null, workMode: null);

        Assert.False(raised);
    }

    /// <summary>A case-only change to <c>name:</c> is still a rename: the display Name genuinely changed even though SQLite and <see cref="PersonaIndex"/> both treat "coo" and "Coo" as equal.</summary>
    [Fact]
    public void Update_CaseOnlyRename_RaisesPersonaRenamed()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(TestPersonas.Identity("coo"), "first");
        var renames = new List<PersonaRenamed>();
        store.PersonaRenamed += renamed => renames.Add(renamed);

        store.Update("coo", TestPersonas.FrontmatterText("Coo", "second"), model: null, effort: null, workMode: null);

        var renamed = Assert.Single(renames);
        Assert.Equal("coo", renamed.OldName);
        Assert.Equal("Coo", renamed.NewName);
    }

    /// <summary>Adding a brand new Persona file is not a rename: its path never existed in the previous index.</summary>
    [Fact]
    public void Add_RaisesNoPersonaRenamed()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(TestPersonas.Identity("coo"), "first");
        var raised = false;
        store.PersonaRenamed += _ => raised = true;

        store.Add(TestPersonas.Identity("vp"), "second");

        Assert.False(raised);
    }

    /// <summary>Deleting a Persona is not a rename: its path disappears from the new index rather than surviving under a different Name.</summary>
    [Fact]
    public void Remove_RaisesNoPersonaRenamed()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(TestPersonas.Identity("coo"), "first");
        var raised = false;
        store.PersonaRenamed += _ => raised = true;

        store.Remove("coo");

        Assert.False(raised);
    }

    /// <summary>
    /// Pins the idempotency the whole detection scheme relies on: after an in-app save, the debounced
    /// FileSystemWatcher rescan (fired here directly rather than waited for) sees the same write the
    /// store already published, so PersonaRenamed must not fire a second time.
    /// </summary>
    [Fact]
    public void Update_ChangingTheName_DoesNotRaisePersonaRenamedAgainOnTheWatchersRedundantRefresh()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(TestPersonas.Identity("coo"), "first");
        var renames = new List<PersonaRenamed>();
        store.PersonaRenamed += renamed => renames.Add(renamed);

        store.Update("coo", TestPersonas.FrontmatterText("vp", "second"), model: null, effort: null, workMode: null);
        store.OnDebounceElapsed(null);

        var renamed = Assert.Single(renames);
        Assert.Equal("coo", renamed.OldName);
        Assert.Equal("vp", renamed.NewName);
    }

    /// <summary>
    /// The watcher's rescan after an in-app Add finds exactly what Add already published, so it must
    /// not raise a second PersonasChanged (a redundant Agents reload and supervisor pass).
    /// </summary>
    [Fact]
    public void Add_FollowedByTheWatchersDebounce_RaisesPersonasChangedOnce()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        var raisedCount = 0;
        store.PersonasChanged += () => Interlocked.Increment(ref raisedCount);

        store.Add(TestPersonas.Identity("coo"), "first");
        store.OnDebounceElapsed(null);

        Assert.Equal(1, raisedCount);
    }

    /// <summary>
    /// Same as the Add case for Update: the watcher's rescan of the store's own write raises nothing.
    /// </summary>
    [Fact]
    public void Update_FollowedByTheWatchersDebounce_RaisesPersonasChangedOnce()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(TestPersonas.Identity("coo"), "first");
        var raisedCount = 0;
        store.PersonasChanged += () => Interlocked.Increment(ref raisedCount);

        store.Update("coo", TestPersonas.FrontmatterText("coo", "second"), model: null, effort: null, workMode: null);
        store.OnDebounceElapsed(null);

        Assert.Equal(1, raisedCount);
    }

    /// <summary>
    /// A file changed on disk by something other than the store (here only its body, which is the
    /// subtlest difference) is still an outside edit and raises PersonasChanged exactly once, however
    /// many times the debounce then fires.
    /// </summary>
    [Fact]
    public void ExternalEdit_FollowedByTheDebounce_RaisesPersonasChangedOnce()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(TestPersonas.Identity("coo"), "first");
        store.OnDebounceElapsed(null);
        var raisedCount = 0;
        store.PersonasChanged += () => Interlocked.Increment(ref raisedCount);

        File.WriteAllText(store.PathFor("coo"), TestPersonas.FrontmatterText("coo", "edited outside the app"));
        store.OnDebounceElapsed(null);
        store.OnDebounceElapsed(null);

        Assert.Equal(1, raisedCount);
    }

    /// <summary>
    /// A file deleted outside the app is still detected: PersonaRemoved fires, strictly before
    /// PersonasChanged.
    /// </summary>
    [Fact]
    public void ExternalDelete_FollowedByTheDebounce_RaisesPersonaRemovedBeforePersonasChanged()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(TestPersonas.Identity("coo"), "first");
        store.OnDebounceElapsed(null);
        var order = new List<string>();
        store.PersonaRemoved += removed => order.Add("PersonaRemoved:" + removed.Name);
        store.PersonasChanged += () => order.Add("PersonasChanged");

        File.Delete(store.PathFor("coo"));
        store.OnDebounceElapsed(null);

        Assert.Equal(["PersonaRemoved:coo", "PersonasChanged"], order);
    }

    /// <summary>
    /// The other door: a file hand-edited outside the app (an editor, a script) bypasses
    /// <see cref="PersonaStore.Update"/> entirely and reaches only the debounced FileSystemWatcher
    /// path, where no old Name is recorded anywhere else - this drives the real watcher, rather than
    /// calling the detection code directly, because that watcher path is exactly what is under test.
    /// </summary>
    [Fact]
    public async Task ExternalFileRename_ThroughTheWatcher_RaisesPersonaRenamed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(TestPersonas.Identity("coo"), "You are the Chief of Staff.");
        var path = store.PathFor("coo");
        var renames = new List<PersonaRenamed>();
        store.PersonaRenamed += renamed => renames.Add(renamed);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.PersonasChanged += () =>
        {
            // Wait for the condition asserted below, not the first event: the Add above still has a
            // debounce pending, which can fire before the rename's own rescan.
            if (renames.Count > 0)
            {
                tcs.TrySetResult();
            }
        };

        await File.WriteAllTextAsync(path, TestPersonas.FrontmatterText("vp", "You are the VP now."), ct);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var registration = cts.Token.Register(() => tcs.TrySetCanceled());
        await tcs.Task;

        var renamed = Assert.Single(renames);
        Assert.Equal("coo", renamed.OldName);
        Assert.Equal("vp", renamed.NewName);
    }

    /// <summary>Changing the frontmatter Name moves the SQLite rows so the old Name's Model/Effort cannot resurrect on a later, unrelated Persona of that Name.</summary>
    [Fact]
    public async Task Update_ChangingTheName_MovesTheModelAndEffortRowsRatherThanLeavingThemBehind()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(TestPersonas.Identity("coo"), "You are the Chief of Staff.", "claude-opus-4", "high");

        var updated = store.Update("coo", TestPersonas.FrontmatterText("vp", "You are the VP."), "claude-opus-4", "high", workMode: null);

        Assert.Equal("vp", updated.Name);
        Assert.Equal("claude-opus-4", updated.Model);
        Assert.Equal("high", updated.Effort);
        Assert.Null(store.Get("coo"));
        var vp = store.Get("vp");
        Assert.NotNull(vp);
        Assert.Equal("claude-opus-4", vp.Model);
        Assert.Equal("high", vp.Effort);

        // Without this, editing name: would silently drop the agent's Model - the exact
        // "silently resurrect an old setting" failure rules.md exists to prevent, run backwards.
        // Add can no longer write a fresh "coo" at a mismatched filename the way Phase 2b's Add
        // did (that mismatch is exactly the bug this phase fixes), and the renamed Persona's OLD
        // file ("coo.md") is still on disk holding "vp"'s content now - so the new "coo" file has
        // to land at a DIFFERENT physical path, written directly (as a hand-authored file would
        // be) and picked up through the watcher, to prove anything about the SQLite rows rather
        // than colliding on the old path.
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.PersonasChanged += () =>
        {
            // Wait for the condition asserted below, not the first event: Update's own file write
            // still arms a debounce that fires after Update returns and before coo-new is scanned.
            if (store.Get("coo") is not null)
            {
                tcs.TrySetResult();
            }
        };
        TestPersonaFiles.Write(new AgentPaths(dir.Options()), "coo-new", TestPersonas.FrontmatterText("coo", "You are a brand new Chief of Staff."));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var registration = cts.Token.Register(() => tcs.TrySetCanceled());
        await tcs.Task;

        var recreated = store.Get("coo");
        Assert.NotNull(recreated);
        Assert.Null(recreated.Model);
        Assert.Null(recreated.Effort);
    }

    /// <summary>
    /// Supersedes Update_OnANestedPersona_WritesBackToTheNestedPath (ADR-0031 retired organisational
    /// Team sub-folders): a definition whose folder name differs from its front-matter Name
    /// (<see cref="Scan_FolderNameDiffersFromName_LoadsWithWarning"/>) still loads, and
    /// <see cref="PersonaStore.Update"/> must write back to that exact file in place rather than
    /// relocate it to a folder matching the Name.
    /// </summary>
    [Fact]
    public async Task Update_DefinitionInAFolderNamedDifferently_WritesBackInPlace()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var paths = new AgentPaths(dir.Options());
        Directory.CreateDirectory(paths.AgentFolder("Old"));
        var definitionPath = Path.Combine(paths.AgentFolder("Old"), "Old.md");
        await File.WriteAllTextAsync(definitionPath, TestPersonas.FrontmatterText("coo", "You are the Chief of Staff."), ct);
        using var store = CreateStore(dir);
        Assert.Equal("coo", Assert.Single(store.ListNames()));

        store.Update("coo", TestPersonas.FrontmatterText("coo", "You are the revised Chief of Staff."), model: null, effort: null, workMode: null);

        Assert.Equal(TestPersonas.FrontmatterText("coo", "You are the revised Chief of Staff."), await File.ReadAllTextAsync(definitionPath, ct));
        Assert.Equal(definitionPath, store.PathFor("coo"));
    }

    [Fact]
    public void Remove_DeletesTheFile()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(TestPersonas.Identity("coo"), "text");
        var path = Path.Combine(dir.Path, "Agents", "coo.md");

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
        store.Add(TestPersonas.Identity("coo"), "text", "claude-opus-4");

        store.Remove("coo");
        var recreated = store.Add(TestPersonas.Identity("coo"), "new text");

        Assert.Null(recreated.Model);
    }

    [Fact]
    public void Remove_AlsoRemovesTheStoredEffort()
    {
        // A stale effort must not resurrect: if it did, re-creating a Persona of the same name
        // would silently inherit an effort nobody chose for it this time.
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(TestPersonas.Identity("coo"), "text", "claude-opus-4", "high");

        store.Remove("coo");
        var recreated = store.Add(TestPersonas.Identity("coo"), "new text");

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
        store.Add(TestPersonas.Identity("coo"), "text");
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
        store.Add(TestPersonas.Identity("coo"), "text");

        var directory = await dir.CreateDirectoryAsync(ct);
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
        store.Add(TestPersonas.Identity("coo"), "text");

        var path = store.PathFor("coo");

        Assert.Equal(Path.Combine(dir.Path, "Agents", "coo", "coo.md"), path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task ExternalFileChange_IsNoticedThroughPersonasChanged()
    {
        // Proves the FileSystemWatcher (not just calls through PersonaStore itself) drives
        // PersonasChanged: this writes a .md file directly to disk, bypassing the store entirely.
        // TestPersonaFiles.Write creates Agents/external/ and then writes into it, which is the
        // Linux watcher gap: the new folder's own Created event must schedule the rescan (see
        // AffectsATeamsFile_ANewAgentFolder_SchedulesARescan). Before that, this was red about one
        // Linux run in six under load.
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.PersonasChanged += () =>
        {
            if (store.ListNames().Contains("external"))
            {
                tcs.TrySetResult();
            }
        };

        TestPersonaFiles.Write(new AgentPaths(dir.Options()), "external", TestPersonas.FrontmatterText("external", "You are External."));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var registration = cts.Token.Register(() => tcs.TrySetCanceled());
        await tcs.Task;

        Assert.Contains("external", store.ListNames());
    }

    /// <summary>
    /// Supersedes TwoPersonaFilesWithTheSameNameInDifferentTeamSubFolders_AreBothRejectedAsACollision
    /// (ADR-0031 retired organisational Team sub-folders): with real front-matter identity, two
    /// definitions in DIFFERENT agent folders that resolve to the same Name are a collision, not
    /// a coincidence to fold away by picking a winner. BOTH are rejected.
    /// </summary>
    [Fact]
    public void TwoDefinitionsInDifferentAgentFoldersWithTheSameName_AreBothRejectedAsACollision()
    {
        using var dir = new TempDataDir();
        var paths = new AgentPaths(dir.Options());
        Directory.CreateDirectory(paths.AgentFolder("Business"));
        Directory.CreateDirectory(paths.AgentFolder("Engineering"));
        var pathA = Path.Combine(paths.AgentFolder("Business"), "Business.md");
        var pathB = Path.Combine(paths.AgentFolder("Engineering"), "Engineering.md");
        File.WriteAllText(pathA, TestPersonas.FrontmatterText("coo", "Business flavour."));
        File.WriteAllText(pathB, TestPersonas.FrontmatterText("coo", "Engineering flavour."));
        using var store = CreateStore(dir);

        Assert.DoesNotContain("coo", store.ListNames());
        Assert.Equal(2, store.RejectedFiles.Count);
        Assert.All(store.RejectedFiles, file => Assert.Contains("coo", file.Reason, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>front-matter Name wins over the filename it happens to be stored under.</summary>
    [Fact]
    public void ListNames_UsesTheFrontMatterNameRatherThanTheFilename()
    {
        using var dir = new TempDataDir();
        TestPersonaFiles.Write(new AgentPaths(dir.Options()), "zzz", TestPersonas.FrontmatterText("Jarvis", "You are Jarvis."));
        using var store = CreateStore(dir);

        var names = store.ListNames();

        Assert.Contains("Jarvis", names);
        Assert.DoesNotContain("zzz", names);
    }

    [Fact]
    public void RejectedFiles_ReportsAMalformedFilesPathAndReason()
    {
        using var dir = new TempDataDir();
        var paths = new AgentPaths(dir.Options());
        TestPersonaFiles.Write(paths, "broken", "---\nTitle: Chief of Staff\nAlias: coo\n---\nYou are the Chief of Staff.");
        var path = paths.DefinitionFile("broken");
        using var store = CreateStore(dir);

        var rejection = Assert.Single(store.RejectedFiles);

        Assert.Equal(path, rejection.Path);
        Assert.Contains("Name", rejection.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Teams_ReturnsTheDistinctSortedTeamNamesAcrossEveryPersona()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(TestPersonas.Identity("coo", teams: ["Household", "Business"]), "body");
        store.Add(TestPersonas.Identity("cto", teams: ["Business", "Engineering"]), "body");

        Assert.Equal(["Business", "Engineering", "Household"], store.Teams);
    }

    [Fact]
    public void ResolveByNameOrAlias_FindsAPersonaByItsAlias()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(new PersonaIdentity("coo", "Chief of Staff", "jar", []), "body");

        var resolved = store.ResolveByNameOrAlias("jar");

        Assert.NotNull(resolved);
        Assert.Equal("coo", resolved.Name);
    }

    /// <summary>
    /// The folder's own Created event is the one signal a new Agent folder is guaranteed to
    /// raise on every platform (on Linux the watch on it is added only after that event is read), so
    /// it alone must schedule a rescan - whatever was written inside it before the watcher caught up
    /// is found from disk.
    /// </summary>
    [Fact]
    public void AffectsATeamsFile_ANewAgentFolder_SchedulesARescan()
    {
        using var dir = new TempDataDir();
        string agentsDir = Path.Combine(dir.Path, "Agents");
        Directory.CreateDirectory(Path.Combine(agentsDir, "Nova"));
        FileSystemEventArgs created = new(WatcherChangeTypes.Created, agentsDir, "Nova");

        Assert.True(PersonaStore.AffectsATeamsFile(agentsDir, created));
    }

    /// <summary>
    /// Only a folder one segment deep (a new Agent folder) can hold a definition, so creating a
    /// agent's work/ folder, or any folder inside it, is the agent's own work and must not
    /// restart it (corrections-B2 item 13; found by the #83 merge through
    /// Watcher_WriteUnderWork_DoesNotRaisePersonasChanged).
    /// </summary>
    /// <param name="relativeFolder">The created folder, relative to the Agents root.</param>
    [Theory]
    [InlineData("Nova/work")]
    [InlineData("Nova/work/drafts")]
    public void AffectsATeamsFile_ANewFolderBelowAAgentFolder_IsFalse(string relativeFolder)
    {
        ArgumentNullException.ThrowIfNull(relativeFolder);
        using var dir = new TempDataDir();
        string agentsDir = Path.Combine(dir.Path, "Agents");
        string relative = relativeFolder.Replace('/', Path.DirectorySeparatorChar);
        Directory.CreateDirectory(Path.Combine(agentsDir, relative));
        FileSystemEventArgs created = new(WatcherChangeTypes.Created, agentsDir, relative);

        Assert.False(PersonaStore.AffectsATeamsFile(agentsDir, created));
    }

    /// <summary>
    /// The watcher's filter was widened from "*.md" to "*" so a directory rename is not silently
    /// dropped (see the constructor), which means the handler itself is now what keeps a stray
    /// non-Persona file from churning the debounce. This proves a ".txt" write raises nothing by
    /// writing one, waiting a bounded grace period with no event, then writing a real Persona file
    /// afterward to prove the watcher was alive the whole time and simply ignored the ".txt"
    /// rather than never having had a chance to fire at all.
    /// </summary>
    [Fact]
    public async Task ExternalNonMarkdownFileChange_DoesNotRaisePersonasChanged()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        var teamsDir = Path.Combine(dir.Path, "Agents");

        await AssertRaisesNoPersonasChangedAsync(store, token => File.WriteAllTextAsync(Path.Combine(teamsDir, "notes.txt"), "not a persona", token), ct);

        // Proves the watcher was alive and listening the whole time (rather than this test just
        // never having given it a chance to fire): a genuine Persona file written right after
        // still gets noticed.
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.PersonasChanged += () =>
        {
            if (store.Get("coo") is not null)
            {
                tcs.TrySetResult();
            }
        };
        TestPersonaFiles.Write(new AgentPaths(dir.Options()), "coo", TestPersonas.FrontmatterText("coo", "You are the Chief of Staff."));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var registration = cts.Token.Register(() => tcs.TrySetCanceled());
        await tcs.Task;
    }

    /// <summary>
    /// Runs <paramref name="action"/> and waits a bounded grace period (750&#160;ms - a full
    /// debounce window and then some) to prove it raised no <see cref="PersonaStore.PersonasChanged"/>
    /// event. Shared by <see cref="ExternalNonMarkdownFileChange_DoesNotRaisePersonasChanged"/> and
    /// <see cref="Watcher_WriteUnderWork_DoesNotRaisePersonasChanged"/> (corrections-B2 item 17).
    /// </summary>
    /// <param name="store">The store whose <see cref="PersonaStore.PersonasChanged"/> event must stay silent.</param>
    /// <param name="action">The filesystem change to make before waiting.</param>
    /// <param name="ct">Bounds the delay.</param>
    private static async Task AssertRaisesNoPersonasChangedAsync(PersonaStore store, Func<CancellationToken, Task> action, CancellationToken ct)
    {
        var raised = false;
        store.PersonasChanged += () => raised = true;

        await action(ct);
        await Task.Delay(TimeSpan.FromMilliseconds(750), ct);

        Assert.False(raised);
    }

    /// <summary>
    /// Pins Spec §6.8's "joint" check: two brand-new texts that collide with EACH OTHER on Alias
    /// must both come back with a problem, not just the second one enumerated - the same
    /// "every file on either side of a collision is rejected" rule <see cref="PersonaIndex"/>
    /// already enforces for files on disk, now proven for <see cref="PersonaStore.Check"/>'s
    /// dry run over supplied texts that were never written anywhere.
    /// </summary>
    [Fact]
    public void Check_TwoNewTextsSameAlias_BothRejectedWithReasons()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        var textA = "---\nName: alpha\nTitle: Alpha\nAlias: shared\n---\nAlpha body.";
        var textB = "---\nName: beta\nTitle: Beta\nAlias: shared\n---\nBeta body.";

        var results = store.Check([textA, textB]);

        Assert.Equal(2, results.Count);
        Assert.All(results, result => Assert.Contains("shared", result.Problem, StringComparison.Ordinal));
        Assert.Empty(Directory.GetFiles(Path.Combine(dir.Path, "Agents"), "*.md", SearchOption.AllDirectories));
    }

    /// <summary>
    /// A brand-new text whose Alias equals an EXISTING Persona's Name is rejected - the same
    /// collision <see cref="PersonaIndex.Build"/> enforces across files already on disk, now
    /// caught by <see cref="PersonaStore.Check"/> against the current entries BEFORE
    /// <see cref="PersonaStore.Add"/> ever gets a chance to write the colliding file.
    /// </summary>
    [Fact]
    public void Check_NewTextAliasEqualsExistingName_Rejected()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(TestPersonas.Identity("coo"), "You are the Chief of Staff.");
        var candidateText = "---\nName: vp\nTitle: VP\nAlias: coo\n---\nVP body.";

        var results = store.Check([candidateText]);

        var result = Assert.Single(results);
        Assert.Equal(candidateText, result.Text);
        Assert.Contains("coo", result.Problem, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(dir.Path, "Agents", "vp.md")));
    }

    /// <summary>
    /// A candidate text with a clean identity and no collisions reports no problem, and
    /// <see cref="PersonaStore.Check"/> writes nothing to disk either way - the whole point of a
    /// dry-run check an Agent can call for free before proposing an Agent.
    /// </summary>
    [Fact]
    public void Check_ValidText_NoProblems_NoFileWritten()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        var text = "---\nName: coo\nTitle: Chief of Staff\nAlias: coo\n---\nYou are the Chief of Staff.";

        var results = store.Check([text]);

        var result = Assert.Single(results);
        Assert.Equal(text, result.Text);
        Assert.Null(result.Problem);
        Assert.False(File.Exists(Path.Combine(dir.Path, "Agents", "coo.md")));
        Assert.Empty(store.ListNames());
    }

    /// <summary>
    /// Two brand-new texts that collide with EACH OTHER on Name (not merely Alias) must both be
    /// rejected, AND each reason must actually name the OTHER text's synthetic path - a naive
    /// "{Name}.md" synthetic path for every occurrence would collapse both texts onto the
    /// identical Path, and <see cref="PersonaIndex"/>'s "others" exclusion (which compares Path)
    /// would then see no "other" file at all for either one, producing a reason with an empty
    /// "used by" list rather than naming the real collision. Also proves <see cref="PersonaStore.Check"/>
    /// never drops or merges a result even when two inputs share a synthetic path: two texts in,
    /// two results out, in the same order.
    /// </summary>
    [Fact]
    public void Check_TwoNewTextsSameName_BothRejectedNamingEachOther()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        var textA = "---\nName: coo\nTitle: Alpha\nAlias: alpha\n---\nAlpha body.";
        var textB = "---\nName: coo\nTitle: Beta\nAlias: beta\n---\nBeta body.";

        var results = store.Check([textA, textB]);

        Assert.Equal(2, results.Count);
        Assert.Equal(textA, results[0].Text);
        Assert.Equal(textB, results[1].Text);
        Assert.NotNull(results[0].Problem);
        Assert.NotNull(results[1].Problem);

        // Each reason must name the OTHER text's own synthetic path, proving the two texts did
        // not collapse onto one shared Path with an empty "used by" list.
        Assert.Contains("coo~1.md", results[0].Problem, StringComparison.Ordinal);
        Assert.Contains("coo.md", results[1].Problem, StringComparison.Ordinal);
        Assert.DoesNotContain("used by .", results[0].Problem, StringComparison.Ordinal);
        Assert.DoesNotContain("used by .", results[1].Problem, StringComparison.Ordinal);

        Assert.Empty(Directory.GetFiles(Path.Combine(dir.Path, "Agents"), "*.md", SearchOption.AllDirectories));
    }

    /// <summary>
    /// A brand-new text whose Name equals an EXISTING Persona's own Name is rejected with a reason
    /// that actually names the real file - not the collapsed-path bug the sibling-duplicate test
    /// above guards for a different pair: the candidate's synthetic path used to be the SAME string
    /// as the real file's path (both <c>{teamsDir}/Iris.md</c>), so <see cref="PersonaIndex"/>'s
    /// "others" exclusion (which compares Path) found nothing to name, producing a reason with an
    /// empty "used by" list. <c>PersonaStore.SyntheticPathsFor</c> now gives a text whose
    /// Name already has an entry on disk a <c>~n</c> synthetic path too, the same way it already
    /// did for a second sibling proposing the same Name.
    /// </summary>
    [Fact]
    public void Check_NewTextNameEqualsExistingPersonaName_NamesTheFile()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(TestPersonas.Identity("Iris"), "Existing Iris body.");
        var candidateText = "---\nName: Iris\nTitle: Reviewer\nAlias: new-iris\n---\nCandidate body.";

        var results = store.Check([candidateText]);

        var result = Assert.Single(results);
        Assert.Equal(candidateText, result.Text);
        Assert.NotNull(result.Problem);
        Assert.Contains("Iris.md", result.Problem, StringComparison.Ordinal);
        Assert.DoesNotContain("used by .", result.Problem, StringComparison.Ordinal);
    }

    /// <summary>
    /// Pins Spec §14 D-14: <see cref="PersonaStore.Add"/>'s exists check and its
    /// <c>ValidateCandidate</c> call both run against <c>this.index</c>, a snapshot only refreshed
    /// AFTER a write completes - so two concurrent Adds proposing DIFFERENT Names but the SAME
    /// Alias can both read the same stale snapshot, both pass validation, and both write, rather
    /// than one being rejected before it ever touches disk. Released together from a
    /// <see cref="Barrier"/> so both threads reach <see cref="PersonaStore.Add"/> at (as close to)
    /// the same instant as possible, and run across 20 iterations, because the race window is
    /// small enough that any single run can get lucky and serialise cleanly on its own.
    /// </summary>
    [Fact]
    public async Task Add_ConcurrentSameAlias_ExactlyOneSucceeds()
    {
        var ct = TestContext.Current.CancellationToken;

        for (var iteration = 0; iteration < 20; iteration++)
        {
            using var dir = new TempDataDir();
            using var store = CreateStore(dir);
            using var barrier = new Barrier(2);

            bool TryAdd(string name)
            {
                barrier.SignalAndWait(ct);
                try
                {
                    store.Add(TestPersonas.Identity(name, alias: "shared"), $"You are {name}.");
                    return true;
                }
                catch (ChatException)
                {
                    return false;
                }
            }

            var first = Task.Run(() => TryAdd("alpha"), ct);
            var second = Task.Run(() => TryAdd("beta"), ct);
            var outcomes = await Task.WhenAll(first, second);

            Assert.True(
                outcomes[0] != outcomes[1],
                $"Iteration {iteration}: expected exactly one success and one ChatException, got [alpha={outcomes[0]}, beta={outcomes[1]}].");
            Assert.Empty(store.RejectedFiles);
        }
    }

    /// <summary>
    /// Pins docs/engineering/known-limits.md's "Third known flake": <see cref="PersonaStore.OnWatcherError"/>
    /// used to call the logger BEFORE taking <c>watchGate</c> and checking <c>disposed</c>, so a late
    /// <see cref="FileSystemWatcher"/> Error event arriving after <see cref="PersonaStore.Dispose"/> -
    /// during host teardown, when the logging provider itself can already be disposed - threw an
    /// unhandled <see cref="ObjectDisposedException"/> on the watcher callback thread and crashed the
    /// process. <see cref="RecordingLogger{T}"/> with <c>throwOnLog</c> stands in for that disposed logging provider: it
    /// throws on every call, so if the handler still reaches the logger after <c>Dispose</c>, this
    /// test surfaces the exact same exception type the real crash did. Invokes the handler directly
    /// (narrowed to <c>internal</c> for exactly this) rather than via reflection or a real watcher
    /// overflow, which cannot be triggered deterministically.
    /// </summary>
    [Fact]
    public void OnWatcherError_AfterDispose_DoesNotLog()
    {
        using var dir = new TempDataDir();
        var throwingLogger = new RecordingLogger<PersonaStore>(throwOnLog: new ObjectDisposedException(nameof(PersonaStore)));
        using var store = new PersonaStore(new AgentPaths(dir.Options()), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), new PersonaWorkModeStore(dir.Options()), throwingLogger);
        store.Dispose();

        var exception = Record.Exception(() => store.OnWatcherError(store, new ErrorEventArgs(new IOException("simulated buffer overflow"))));

        Assert.Null(exception);
    }

    /// <summary>
    /// A watcher debounce that lands after <see cref="PersonaStore.Update"/> has written its file but
    /// before it has published must not publish the rename itself. If it does, the rename is raised
    /// from the timer thread, <c>Update</c> finds nothing to diff and returns, and a caller that reads
    /// the Team Directory right away (the rename cascade's row rename is what it waits on) still sees
    /// the OLD Name - the flake recorded against
    /// <c>PersonaRenameCascadeTests.Rename_RenamesTheAgentRow_AndKeepsItsId</c>. The test holds
    /// <c>Update</c> inside its write lock by keeping a SQLite write lock on the database that
    /// <c>Update</c>'s Model bookkeeping needs, fires the debounce callback while it is stuck there,
    /// and asserts the rename was raised by the thread that called <c>Update</c>.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Update_ADebounceRescanDuringTheWrite_DoesNotStealTheRename()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(TestPersonas.Identity("coo"), "You are the Chief of Staff.");
        var path = store.PathFor("coo");
        var renameThreads = new List<int>();
        store.PersonaRenamed += _ =>
        {
            lock (renameThreads)
            {
                renameThreads.Add(Environment.CurrentManagedThreadId);
            }
        };

        var updateThreadId = 0;
        Task updateTask;
        Task debounceTask;
        var dbPath = Path.Combine(dir.Options().Value.DataDir, "team.db");
        using (var blocker = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}"))
        {
            await blocker.OpenAsync(ct);
            await using (var begin = blocker.CreateCommand())
            {
                begin.CommandText = "BEGIN IMMEDIATE;";
                await begin.ExecuteNonQueryAsync(ct);
            }

            updateTask = Task.Run(
                () =>
                {
                    updateThreadId = Environment.CurrentManagedThreadId;
                    return store.Update("coo", TestPersonas.FrontmatterText("vp", "You are the VP."), "claude-opus-4", null, workMode: null);
                },
                ct);

            // Update has written the file (so the disk holds the new Name) and is now stuck moving the
            // Model row behind the SQLite write lock, still inside its write lock.
            while (!FileHoldsNewName(path))
            {
                await Task.Delay(10, ct);
            }

            debounceTask = Task.Run(() => store.OnDebounceElapsed(null), ct);

            // Bounded, and only to give an UNFIXED debounce the chance to run to completion here: a
            // fixed one is parked on the store's write lock and cannot finish until Update is released.
            _ = await Task.WhenAny(debounceTask, Task.Delay(TimeSpan.FromSeconds(1), ct));

            await using var rollback = blocker.CreateCommand();
            rollback.CommandText = "ROLLBACK;";
            await rollback.ExecuteNonQueryAsync(ct);
        }

        await updateTask;
        await debounceTask;

        var thread = Assert.Single(renameThreads);
        Assert.Equal(updateThreadId, thread);
    }

    /// <summary>Whether the Persona file at <paramref name="path"/> already carries the Name <c>vp</c> - false while <see cref="PersonaStore.Update"/> is still writing it.</summary>
    /// <param name="path">The Persona file to read.</param>
    /// <returns><see langword="true"/> once the file's text names <c>vp</c>.</returns>
    private static bool FileHoldsNewName(string path)
    {
        try
        {
            return File.ReadAllText(path).Contains("vp", StringComparison.Ordinal);
        }
        catch (IOException)
        {
            // Update is mid-write and holds the file; the caller polls again.
            return false;
        }
    }

    // ADR-0031 layout

    /// <summary>A definition file directly inside its own agent folder (Spec §6.15) loads normally.</summary>
    [Fact]
    public void Scan_DefinitionInAgentFolder_Loads()
    {
        using var dir = new TempDataDir();
        var paths = new AgentPaths(dir.Options());
        TestPersonaFiles.Write(paths, "Nova", TestPersonas.FrontmatterText("Nova", "You are Nova."));

        using var store = CreateStore(dir);

        Assert.Equal(["Nova"], store.ListNames());
    }

    /// <summary>
    /// Spec §6.15's one-level scan: markdown nested under an agent's <c>work/</c> sub-folder is
    /// neither a Persona nor a rejected file - it is never scanned at all, however deep it sits.
    /// </summary>
    [Fact]
    public void Scan_MarkdownUnderWork_IsIgnored()
    {
        using var dir = new TempDataDir();
        var paths = new AgentPaths(dir.Options());
        TestPersonaFiles.Write(paths, "Nova", TestPersonas.FrontmatterText("Nova", "You are Nova."));
        var workDir = paths.WorkDir("Nova");
        Directory.CreateDirectory(Path.Combine(workDir, "memory"));
        File.WriteAllText(Path.Combine(workDir, "memory", "fact.md"), "Some remembered fact.");
        File.WriteAllText(Path.Combine(workDir, "draft.md"), "A draft.");

        using var store = CreateStore(dir);

        Assert.Equal(["Nova"], store.ListNames());
        Assert.Empty(store.RejectedFiles);
    }

    /// <summary>A definition file must sit inside its own agent folder - one directly under the Agents root is rejected, never loaded.</summary>
    [Fact]
    public void Scan_FileDirectlyInRoot_IsRejected()
    {
        using var dir = new TempDataDir();
        var paths = new AgentPaths(dir.Options());
        Directory.CreateDirectory(paths.DefinitionsRoot);
        var strayPath = Path.Combine(paths.DefinitionsRoot, "stray.md");
        File.WriteAllText(strayPath, TestPersonas.FrontmatterText("Stray", "You are Stray."));

        using var store = CreateStore(dir);

        Assert.Empty(store.ListNames());
        var rejection = Assert.Single(store.RejectedFiles);
        Assert.Equal(strayPath, rejection.Path);
        Assert.Equal("A definition must be inside its agent's folder.", rejection.Reason);
    }

    /// <summary>
    /// corrections-B2 item 14: a second ".md" beside an agent's own definition is rejected, as a
    /// duplicate is today, but the folder's own definition - the file whose stem matches the folder
    /// Name - keeps loading rather than being pulled down with it.
    /// </summary>
    [Fact]
    public void Scan_SecondMarkdownInAgentFolder_IsRejected()
    {
        using var dir = new TempDataDir();
        var paths = new AgentPaths(dir.Options());
        TestPersonaFiles.Write(paths, "Nova", TestPersonas.FrontmatterText("Nova", "You are Nova."));
        var notesPath = Path.Combine(paths.AgentFolder("Nova"), "notes.md");
        File.WriteAllText(notesPath, TestPersonas.FrontmatterText("NovaNotes", "Some other notes."));

        using var store = CreateStore(dir);

        Assert.Equal(["Nova"], store.ListNames());
        var rejection = Assert.Single(store.RejectedFiles);
        Assert.Equal(notesPath, rejection.Path);
        Assert.Equal("An agent folder holds only its definition; use work/.", rejection.Reason);
    }

    /// <summary>
    /// corrections-B2 item 12: a definition still loads under its front-matter Name even when its
    /// folder is named differently, but it now shows up in <see cref="PersonaStore.FolderWarnings"/>
    /// so the mismatch can be surfaced.
    /// </summary>
    [Fact]
    public void Scan_FolderNameDiffersFromName_LoadsWithWarning()
    {
        using var dir = new TempDataDir();
        var paths = new AgentPaths(dir.Options());
        Directory.CreateDirectory(paths.AgentFolder("Old"));
        var definitionPath = Path.Combine(paths.AgentFolder("Old"), "Old.md");
        File.WriteAllText(definitionPath, TestPersonas.FrontmatterText("Nova", "You are Nova."));

        using var store = CreateStore(dir);

        Assert.Equal(["Nova"], store.ListNames());
        var warning = Assert.Single(store.FolderWarnings);
        Assert.Equal(definitionPath, warning.Path);
        Assert.Equal("Its folder is named 'Old', not 'Nova'.", warning.Reason);
    }

    /// <summary>Spec §6.15: the watcher must not react to a change made inside an agent's own <c>work/</c> folder.</summary>
    [Fact]
    public async Task Watcher_WriteUnderWork_DoesNotRaisePersonasChanged()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var paths = new AgentPaths(dir.Options());
        TestPersonaFiles.Write(paths, "Nova", TestPersonas.FrontmatterText("Nova", "You are Nova."));
        using var store = CreateStore(dir);
        var workFile = Path.Combine(paths.WorkDir("Nova"), "a.md");
        Directory.CreateDirectory(paths.WorkDir("Nova"));

        await AssertRaisesNoPersonasChangedAsync(store, token => File.WriteAllTextAsync(workFile, "Not a persona.", token), ct);
    }

    /// <summary>
    /// Manager's review of part (a): <see cref="PersonaStore"/>'s watcher filter checked only
    /// <see cref="FileSystemEventArgs.FullPath"/>, so a <see cref="RenamedEventArgs"/> whose
    /// <see cref="RenamedEventArgs.OldFullPath"/> is the agent's definition (at most 2 segments
    /// below <c>DefinitionsRoot</c>) but whose new path is 3 or more segments deep - moved into
    /// <c>work/</c> - was silently ignored. That left the agent still "loaded" from a file that
    /// no longer exists.
    /// </summary>
    [Fact]
    public async Task Watcher_DefinitionMovedIntoWork_RaisesPersonasChanged()
    {
        using var dir = new TempDataDir();
        var paths = new AgentPaths(dir.Options());
        TestPersonaFiles.Write(paths, "Nova", TestPersonas.FrontmatterText("Nova", "You are Nova."));
        using var store = CreateStore(dir);
        Directory.CreateDirectory(paths.WorkDir("Nova"));

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.PersonasChanged += () =>
        {
            if (!store.ListNames().Contains("Nova"))
            {
                tcs.TrySetResult();
            }
        };

        File.Move(paths.DefinitionFile("Nova"), Path.Combine(paths.WorkDir("Nova"), "Nova.md"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var registration = cts.Token.Register(() => tcs.TrySetCanceled());
        await tcs.Task;

        // The rescan the event triggers proves it fired: Nova's folder now holds no definition, so
        // the moved-away agent is gone rather than served stale from a file that no longer exists.
        Assert.DoesNotContain("Nova", store.ListNames());
    }

    /// <summary>
    /// AffectsATeamsFile detects a rename from the definition file into the work directory: the old
    /// path (at most 2 segments) qualifies even though the new path is deeper.
    /// </summary>
    [Fact]
    public void AffectsATeamsFile_RenameFromDefinitionIntoWork_IsTrue()
    {
        string root = Path.Combine(Path.GetTempPath(), "T");
        string novaFolder = Path.Combine(root, "Nova");
        string workFolder = Path.Combine(novaFolder, "work");
        string oldPath = Path.Combine(novaFolder, "Nova.md");
        string newPath = Path.Combine(workFolder, "Nova.md");
        RenamedEventArgs args = new(WatcherChangeTypes.Renamed, root, Path.Combine("Nova", "work", "Nova.md"), Path.Combine("Nova", "Nova.md"));

        Assert.Equal(oldPath, args.OldFullPath);
        Assert.Equal(newPath, args.FullPath);

        bool result = PersonaStore.AffectsATeamsFile(root, args);

        Assert.True(result);
    }

    /// <summary>
    /// AffectsATeamsFile returns false for a rename within the work directory (both old and new paths
    /// are deeper than 2 segments).
    /// </summary>
    [Fact]
    public void AffectsATeamsFile_RenameDeepToDeep_IsFalse()
    {
        string root = Path.Combine(Path.GetTempPath(), "T");
        string oldPath = Path.Combine(root, "Nova", "work", "a.md");
        string newPath = Path.Combine(root, "Nova", "work", "b.md");
        RenamedEventArgs args = new(WatcherChangeTypes.Renamed, newPath, "b.md", oldPath);

        bool result = PersonaStore.AffectsATeamsFile(root, args);

        Assert.False(result);
    }

    /// <summary>
    /// corrections-B2 item 15: every file under <c>Agents/_unsorted/</c> is a rejected file with
    /// this fixed reason, recursively - the migration only moved it there; the scan is what decides
    /// what it is, and it never becomes a Persona regardless of how deep it sits.
    /// </summary>
    [Fact]
    public void Scan_UnsortedFolder_EveryFileRejectedRecursively()
    {
        using var dir = new TempDataDir();
        var paths = new AgentPaths(dir.Options());
        var unsortedDir = Path.Combine(paths.DefinitionsRoot, "_unsorted");
        Directory.CreateDirectory(Path.Combine(unsortedDir, "deep"));
        var shallowPath = Path.Combine(unsortedDir, "Nova.md");
        var deepPath = Path.Combine(unsortedDir, "deep", "x.md");
        File.WriteAllText(shallowPath, TestPersonas.FrontmatterText("Nova", "You are Nova."));
        File.WriteAllText(deepPath, "Some other text.");

        using var store = CreateStore(dir);

        Assert.Empty(store.ListNames());
        List<(string Path, string Reason)> expected =
        [
            (shallowPath, "Moved here by the layout migration."),
            (deepPath, "Moved here by the layout migration."),
        ];
        Assert.Equal(
            expected.OrderBy(pair => pair.Path, StringComparer.Ordinal),
            store.RejectedFiles.Select(file => (file.Path, file.Reason)).OrderBy(pair => pair.Path, StringComparer.Ordinal));
    }

    /// <summary>
    /// corrections-B2 item 15: an underscore- or dot-prefixed folder OTHER than <c>_unsorted</c> -
    /// an organisational <c>_drafts</c>, or an adapter's own <c>.claude</c> - is skipped entirely by
    /// the scan: no Persona, no rejected file.
    /// </summary>
    [Fact]
    public void Scan_UnderscoreAndDotFolders_Skipped()
    {
        using var dir = new TempDataDir();
        var paths = new AgentPaths(dir.Options());
        var draftsNovaDir = Path.Combine(paths.DefinitionsRoot, "_drafts", "Nova");
        var claudeDir = Path.Combine(paths.DefinitionsRoot, ".claude");
        Directory.CreateDirectory(draftsNovaDir);
        Directory.CreateDirectory(claudeDir);
        File.WriteAllText(Path.Combine(draftsNovaDir, "Nova.md"), TestPersonas.FrontmatterText("Nova", "You are Nova."));
        File.WriteAllText(Path.Combine(claudeDir, "x.md"), "Adapter housekeeping.");

        using var store = CreateStore(dir);

        Assert.Empty(store.ListNames());
        Assert.Empty(store.RejectedFiles);
    }

    /// <summary>
    /// corrections-B2 item 13: the watcher's "at most 2 segments" rule, not "ignore segments equal
    /// to <c>work</c>" - an agent genuinely named "work" must still load, since the plan's original
    /// wording would have hidden it.
    /// </summary>
    [Fact]
    public void Scan_AgentNamedWork_Loads()
    {
        using var dir = new TempDataDir();
        var paths = new AgentPaths(dir.Options());
        TestPersonaFiles.Write(paths, "work", TestPersonas.FrontmatterText("work", "You are named work."));

        using var store = CreateStore(dir);

        Assert.Equal(["work"], store.ListNames());
    }

    /// <summary>
    /// corrections-B2 item 13's distinguishing case for the watcher: an agent folder named "work"
    /// is exactly 2 segments deep (<c>work/work.md</c>), so it must still raise
    /// <see cref="PersonaStore.PersonasChanged"/> when edited, unlike a real <c>&lt;Name&gt;/work/*</c>
    /// path 3 segments deep.
    /// </summary>
    [Fact]
    public async Task Watcher_DefinitionOfAgentNamedWork_RaisesPersonasChanged()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var paths = new AgentPaths(dir.Options());
        TestPersonaFiles.Write(paths, "work", TestPersonas.FrontmatterText("work", "You are named work."));
        using var store = CreateStore(dir);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.PersonasChanged += () => tcs.TrySetResult();

        await File.WriteAllTextAsync(paths.DefinitionFile("work"), TestPersonas.FrontmatterText("work", "You are named work, revised."), ct);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var registration = cts.Token.Register(() => tcs.TrySetCanceled());
        await tcs.Task;

        Assert.Contains("work", store.ListNames());
    }

    /// <summary>Spec §6.15: <see cref="PersonaStore.Add"/> writes a brand-new Persona into its own agent folder, never flat at the Agents root.</summary>
    [Fact]
    public void Add_WritesIntoAgentFolder()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        store.Add(TestPersonas.Identity("Nova"), "You are Nova.");

        var expectedPath = Path.Combine(dir.Path, "Agents", "Nova", "Nova.md");
        Assert.Equal(expectedPath, store.PathFor("Nova"));
        Assert.True(File.Exists(expectedPath));
    }

    /// <summary>
    /// corrections-B2 item 19: a Persona whose folder moved externally - a Path change with an
    /// unchanged frontmatter Name - must not read as a removal. Drives the scenario through a
    /// bare <see cref="PersonaStore"/> with no <see cref="PersonaRenameCascade"/> attached, moving
    /// the folder and renaming the file by hand exactly as an external actor would, then forcing a
    /// rescan directly rather than waiting on the watcher's debounce.
    /// </summary>
    [Fact]
    public void Rescan_PathChangedNameSame_DoesNotRaisePersonaRemoved()
    {
        using var dir = new TempDataDir();
        var paths = new AgentPaths(dir.Options());
        TestPersonaFiles.Write(paths, "Nova", TestPersonas.FrontmatterText("Nova", "You are Nova."));
        using var store = CreateStore(dir);
        List<string> removedNames = [];
        store.PersonaRemoved += removed => removedNames.Add(removed.Name);

        Directory.Move(paths.AgentFolder("Nova"), paths.AgentFolder("Nova2"));
        File.Move(Path.Combine(paths.AgentFolder("Nova2"), "Nova.md"), Path.Combine(paths.AgentFolder("Nova2"), "Nova2.md"));
        store.RescanNow();

        Assert.Empty(removedNames);
        Assert.Contains("Nova", store.ListNames());
    }

    /// <summary>
    /// The defect this test pins: the debounce timer callback ran on a
    /// <see cref="System.Threading.Timer"/> callback and let an <see cref="IOException"/> from a
    /// definition file locked at that instant escape the thread pool, which killed the process. The
    /// settled fix aborts the debounced rebuild instead of publishing an index without the locked
    /// Persona (which would raise <see cref="PersonaStore.PersonaRemoved"/> and cascade-delete its
    /// state): the current index is kept, nothing is raised, and the debounce timer retries. Holds
    /// <c>Nova.md</c> open with <see cref="FileShare.None"/>, touches a second definition to trigger
    /// the watcher, and proves the process is still alive and Nova is untouched by reaching the
    /// asserts below the gate; then releases the handle and proves the store catches up.
    /// </summary>
    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task Watcher_DefinitionFileLockedDuringRebuild_KeepsCurrentIndexAndRetries()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("FileShare.None does not reliably lock a file against reads on this platform.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var paths = new AgentPaths(dir.Options());
        TestPersonaFiles.Write(paths, "Nova", TestPersonas.FrontmatterText("Nova", "You are Nova."));
        using var store = CreateStore(dir);
        List<string> removedNames = [];
        store.PersonaRemoved += removed => removedNames.Add(removed.Name);
        var novaPath = Path.Combine(paths.AgentFolder("Nova"), "Nova.md");

        using (var handle = new FileStream(novaPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            TestPersonaFiles.Write(paths, "Zed", TestPersonas.FrontmatterText("Zed", "You are Zed."));
            await Task.Delay(TimeSpan.FromMilliseconds(750), ct);

            // Reaching this line proves the debounce callback did not crash the process.
            Assert.Empty(removedNames);
            Assert.Contains("Nova", store.ListNames());
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.PersonasChanged += () =>
        {
            if (store.ListNames().Contains("Zed"))
            {
                tcs.TrySetResult();
            }
        };
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var registration = cts.Token.Register(() => tcs.TrySetCanceled());
        await tcs.Task;

        Assert.Contains("Zed", store.ListNames());
        Assert.Empty(removedNames);
    }

    /// <summary>
    /// The constructor's first scan has no previous index to protect, so a locked definition file is
    /// treated as a rejected file - matching every other unreadable-file case - rather than aborting
    /// startup, and the next watcher event rescans it once the lock clears.
    /// </summary>
    [Fact]
    [SupportedOSPlatform("windows")]
    public void Construct_DefinitionFileLockedAtStartup_IsRejectedWithReason()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("FileShare.None does not reliably lock a file against reads on this platform.");
            return;
        }

        using var dir = new TempDataDir();
        var paths = new AgentPaths(dir.Options());
        TestPersonaFiles.Write(paths, "Nova", TestPersonas.FrontmatterText("Nova", "You are Nova."));
        var novaPath = Path.Combine(paths.AgentFolder("Nova"), "Nova.md");

        using var handle = new FileStream(novaPath, FileMode.Open, FileAccess.Read, FileShare.None);
        using var store = CreateStore(dir);

        Assert.Empty(store.ListNames());
        var rejection = Assert.Single(store.RejectedFiles);
        Assert.Equal(novaPath, rejection.Path);
        Assert.Equal("Couldn't read this file; it's in use.", rejection.Reason);
    }

    private static PersonaStore CreateStore(TempDataDir dir)
    {
        return dir.CreatePersonaStore();
    }
}
