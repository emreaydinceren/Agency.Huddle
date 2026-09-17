using Microsoft.Extensions.Logging.Abstractions;
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

        store.Add(Identity("coo"), "You are the Chief of Staff.");

        var path = Path.Combine(dir.Path, "Teams", "coo.md");
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

        store.Add(Identity("Chief of Staff"), "You keep the team honest.");

        var path = Path.Combine(dir.Path, "Teams", "Chief of Staff.md");
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

        var path = Path.Combine(dir.Path, "Teams", "coo.md");
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
        Assert.Throws<ChatException>(() => store.Add(Identity("../escape"), "x"));
        Assert.Throws<ChatException>(() => store.Add(Identity("sub/coo"), "x"));
        Assert.Throws<ChatException>(() => store.Add(Identity("trailing "), "x"));
    }

    /// <summary>An Alias never reaches a path, but Add guards it with the same NameRules check as Name - both are identifiers a caller could otherwise write something invalid into.</summary>
    [Fact]
    public void Add_RejectsInvalidAlias()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        var ex = Assert.Throws<ChatException>(() => store.Add(Identity("coo", alias: "bad/alias"), "text"));

        Assert.Equal(ErrorCodes.BadMessage, ex.Code);
        Assert.False(File.Exists(Path.Combine(dir.Path, "Teams", "coo.md")));
    }

    [Fact]
    public void Get_ReturnsFileText()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(Identity("coo"), "You are the Chief of Staff.");

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
        var teamsDir = Path.Combine(dir.Path, "Teams");
        Directory.CreateDirectory(teamsDir);
        File.WriteAllText(Path.Combine(teamsDir, "coo.md"), PersonaText("coo", "You are the Chief of Staff."));
        var models = new PersonaModelStore(options);
        models.Set("coo", "claude-opus-4");
        using var store = new PersonaStore(options, models, new PersonaEffortStore(options), NullLogger<PersonaStore>.Instance);

        var persona = store.Get("coo");

        Assert.NotNull(persona);
        Assert.Equal(PersonaText("coo", "You are the Chief of Staff."), persona.Text);
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
        var teamsDir = Path.Combine(dir.Path, "Teams");
        Directory.CreateDirectory(teamsDir);
        File.WriteAllText(Path.Combine(teamsDir, "coo.md"), PersonaText("coo", "You are the Chief of Staff."));
        var efforts = new PersonaEffortStore(options);
        efforts.Set("coo", "high");
        using var store = new PersonaStore(options, new PersonaModelStore(options), efforts, NullLogger<PersonaStore>.Instance);

        var persona = store.Get("coo");

        Assert.NotNull(persona);
        Assert.Equal(PersonaText("coo", "You are the Chief of Staff."), persona.Text);
        Assert.Equal("high", persona.Effort);
    }

    /// <summary>A file whose frontmatter carries an <c>adapter:</c> value round-trips it through <see cref="PersonaStore.Get(string)"/> onto <see cref="Persona.Adapter"/> - the file-to-database join <see cref="PersonaStore.Get(string)"/> performs (Spec §7.3).</summary>
    [Fact]
    public void Get_ReturnsTheAdapterFromFrontmatter()
    {
        using var dir = new TempDataDir();
        var options = dir.Options();
        var teamsDir = Path.Combine(dir.Path, "Teams");
        Directory.CreateDirectory(teamsDir);
        File.WriteAllText(Path.Combine(teamsDir, "coo.md"), PersonaText("coo", "You are the Chief of Staff.", "agency"));
        using var store = new PersonaStore(options, new PersonaModelStore(options), new PersonaEffortStore(options), NullLogger<PersonaStore>.Instance);

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
        store.Add(Identity("coo"), "You are the Chief of Staff.");

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
        var teamsDir = Path.Combine(dir.Path, "Teams");
        Directory.CreateDirectory(teamsDir);
        var text = PersonaText("coo", "first", "agency");
        File.WriteAllText(Path.Combine(teamsDir, "coo.md"), text);
        using var store = new PersonaStore(options, new PersonaModelStore(options), new PersonaEffortStore(options), NullLogger<PersonaStore>.Instance);

        var updated = store.Update("coo", text, model: null, effort: null);

        Assert.Equal("agency", updated.Adapter);
        Assert.Equal("agency", store.Get("coo")!.Adapter);
    }

    [Fact]
    public void ListNames_ReturnsNamesWithoutExtension()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(Identity("beta"), "text beta");
        store.Add(Identity("alpha"), "text alpha");

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

        var ex = Assert.Throws<ChatException>(() => store.Add(Identity("../evil"), "text"));

        Assert.Equal(ErrorCodes.BadMessage, ex.Code);
        Assert.False(File.Exists(Path.Combine(dir.Path, "Teams", "evil.md")));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(dir.Path)!, "evil.md")));
    }

    [Fact]
    public void Add_RejectsDuplicate()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(Identity("coo"), "first");

        // Fails on the physical file already existing at "coo.md" - a check that runs before, and
        // independently of, whether "second" would itself load cleanly.
        var ex = Assert.Throws<ChatException>(() => store.Add(Identity("coo"), "second"));

        Assert.Equal(ErrorCodes.BadMessage, ex.Code);
    }

    [Fact]
    public void Add_RejectsBlankText()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        var ex = Assert.Throws<ChatException>(() => store.Add(Identity("coo"), "   "));

        Assert.Equal(ErrorCodes.BadMessage, ex.Code);
    }

    /// <summary>A blank Title cannot load, so it must be rejected up front rather than written and then silently invisible - the one required field Add itself does not pre-validate before composing, relying on ValidateCandidate instead.</summary>
    [Fact]
    public void Add_RejectsBlankTitle()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        var ex = Assert.Throws<ChatException>(() => store.Add(Identity("coo", title: "   "), "text"));

        Assert.Equal(ErrorCodes.BadMessage, ex.Code);
        Assert.False(File.Exists(Path.Combine(dir.Path, "Teams", "coo.md")));
    }

    [Fact]
    public void Add_WithAModel_StoresIt()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        var persona = store.Add(Identity("coo"), "text", "claude-opus-4");

        Assert.Equal("claude-opus-4", persona.Model);
        Assert.Equal("claude-opus-4", store.Get("coo")!.Model);
    }

    [Fact]
    public void Add_WithoutAModel_LeavesItUnset()
    {
        // The agent-default case: a new Persona has no model until one is explicitly chosen.
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        var persona = store.Add(Identity("coo"), "text");

        Assert.Null(persona.Model);
        Assert.Null(store.Get("coo")!.Model);
    }

    [Fact]
    public void Add_WithAnEffort_StoresIt()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        var persona = store.Add(Identity("coo"), "text", "claude-opus-4", "high");

        Assert.Equal("high", persona.Effort);
        Assert.Equal("high", store.Get("coo")!.Effort);
    }

    [Fact]
    public void Add_WithoutAnEffort_LeavesItUnset()
    {
        // The model-default case: a new Persona has no effort until one is explicitly chosen.
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);

        var persona = store.Add(Identity("coo"), "text");

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

        store.Add(Identity("coo"), "text");

        Assert.True(raised);
    }

    [Fact]
    public void Update_OverwritesTheFile()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(Identity("coo"), "first");

        var updated = store.Update("coo", PersonaText("coo", "second"), model: null, effort: null);

        Assert.Equal(PersonaText("coo", "second"), updated.Text);
        var path = Path.Combine(dir.Path, "Teams", "coo.md");
        Assert.Equal(PersonaText("coo", "second"), File.ReadAllText(path));
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
        var added = store.Add(Identity("coo"), "first");

        var ex = Assert.Throws<ChatException>(() => store.Update("coo", "   ", model: null, effort: null));

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
        store.Add(Identity("coo"), "You are the Chief of Staff.");
        var path = store.PathFor("coo");
        var before = await File.ReadAllBytesAsync(path, ct);

        var ex = Assert.Throws<ChatException>(() => store.Update("coo", "You are the Chief of Staff, but with no frontmatter at all now.", model: null, effort: null));

        Assert.Equal(ErrorCodes.BadMessage, ex.Code);
        var after = await File.ReadAllBytesAsync(path, ct);
        Assert.Equal(before, after);
    }

    [Fact]
    public void Update_RaisesPersonasChanged()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(Identity("coo"), "first");
        var raised = false;
        store.PersonasChanged += () => raised = true;

        store.Update("coo", PersonaText("coo", "second"), model: null, effort: null);

        Assert.True(raised);
    }

    [Fact]
    public void Update_ChangingOnlyTheModel_KeepsTheText()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(Identity("coo"), "unchanged text");

        var updated = store.Update("coo", PersonaText("coo", "unchanged text"), "claude-opus-4", effort: null);

        Assert.Equal(PersonaText("coo", "unchanged text"), updated.Text);
        Assert.Equal("claude-opus-4", updated.Model);
        var path = Path.Combine(dir.Path, "Teams", "coo.md");
        Assert.Equal(PersonaText("coo", "unchanged text"), File.ReadAllText(path));
    }

    [Fact]
    public void Update_ChangingOnlyTheEffort_KeepsTheTextAndTheModel()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(Identity("coo"), "unchanged text", "claude-opus-4");

        var updated = store.Update("coo", PersonaText("coo", "unchanged text"), "claude-opus-4", "high");

        Assert.Equal(PersonaText("coo", "unchanged text"), updated.Text);
        Assert.Equal("claude-opus-4", updated.Model);
        Assert.Equal("high", updated.Effort);
        var path = Path.Combine(dir.Path, "Teams", "coo.md");
        Assert.Equal(PersonaText("coo", "unchanged text"), File.ReadAllText(path));
    }

    [Fact]
    public void Update_RaisesPersonasChangedOnce()
    {
        // The three-restarts/three-"node"-spawns guard: writing the file, the model and the effort
        // must not each raise their own PersonasChanged, or PersonaSupervisor would restart the
        // runner three times for a single save.
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(Identity("coo"), "first");
        var raisedCount = 0;
        void Count() => raisedCount++;
        store.PersonasChanged += Count;

        store.Update("coo", PersonaText("coo", "second"), "claude-opus-4", "high");

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
        store.Add(Identity("coo"), "first");
        var renames = new List<PersonaRenamed>();
        store.PersonaRenamed += renamed => renames.Add(renamed);

        store.Update("coo", PersonaText("vp", "second"), model: null, effort: null);

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
        store.Add(Identity("coo"), "first");
        var order = new List<string>();
        store.PersonaRenamed += _ => order.Add("PersonaRenamed");
        store.PersonasChanged += () => order.Add("PersonasChanged");

        store.Update("coo", PersonaText("vp", "second"), model: null, effort: null);

        Assert.Equal(["PersonaRenamed", "PersonasChanged"], order);
    }

    /// <summary>Editing the body without touching <c>name:</c> is an ordinary edit, not a rename.</summary>
    [Fact]
    public void Update_ChangingOnlyTheText_RaisesNoPersonaRenamed()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(Identity("coo"), "first");
        var raised = false;
        store.PersonaRenamed += _ => raised = true;

        store.Update("coo", PersonaText("coo", "second"), model: null, effort: null);

        Assert.False(raised);
    }

    /// <summary>A case-only change to <c>name:</c> is still a rename: the display Name genuinely changed even though SQLite and <see cref="PersonaIndex"/> both treat "coo" and "Coo" as equal.</summary>
    [Fact]
    public void Update_CaseOnlyRename_RaisesPersonaRenamed()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(Identity("coo"), "first");
        var renames = new List<PersonaRenamed>();
        store.PersonaRenamed += renamed => renames.Add(renamed);

        store.Update("coo", PersonaText("Coo", "second"), model: null, effort: null);

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
        store.Add(Identity("coo"), "first");
        var raised = false;
        store.PersonaRenamed += _ => raised = true;

        store.Add(Identity("vp"), "second");

        Assert.False(raised);
    }

    /// <summary>Deleting a Persona is not a rename: its path disappears from the new index rather than surviving under a different Name.</summary>
    [Fact]
    public void Remove_RaisesNoPersonaRenamed()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(Identity("coo"), "first");
        var raised = false;
        store.PersonaRenamed += _ => raised = true;

        store.Remove("coo");

        Assert.False(raised);
    }

    /// <summary>
    /// Pins the idempotency the whole detection scheme relies on: a single save raises
    /// PersonasChanged twice (see <see cref="Update_RaisesPersonasChangedOnce"/>'s comment) - once
    /// synchronously from Update, once ~500&#160;ms later from the debounced FileSystemWatcher noticing
    /// the same write. By the time the second, redundant refresh runs, both the previous and the new
    /// index already agree on the renamed path's Name, so PersonaRenamed must not fire a second time.
    /// </summary>
    [Fact]
    public async Task Update_ChangingTheName_DoesNotRaisePersonaRenamedAgainOnTheWatchersRedundantRefresh()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(Identity("coo"), "first");
        var renames = new List<PersonaRenamed>();
        store.PersonaRenamed += renamed => renames.Add(renamed);

        var secondPersonasChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var changedCount = 0;
        store.PersonasChanged += () =>
        {
            changedCount++;
            if (changedCount == 2)
            {
                secondPersonasChanged.TrySetResult();
            }
        };

        store.Update("coo", PersonaText("vp", "second"), model: null, effort: null);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var registration = cts.Token.Register(() => secondPersonasChanged.TrySetCanceled());
        await secondPersonasChanged.Task;

        var renamed = Assert.Single(renames);
        Assert.Equal("coo", renamed.OldName);
        Assert.Equal("vp", renamed.NewName);
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
        store.Add(Identity("coo"), "You are the Chief of Staff.");
        var path = store.PathFor("coo");
        var renames = new List<PersonaRenamed>();
        store.PersonaRenamed += renamed => renames.Add(renamed);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.PersonasChanged += () => tcs.TrySetResult();

        await File.WriteAllTextAsync(path, PersonaText("vp", "You are the VP now."), ct);

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
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(Identity("coo"), "You are the Chief of Staff.", "claude-opus-4", "high");

        var updated = store.Update("coo", PersonaText("vp", "You are the VP."), "claude-opus-4", "high");

        Assert.Equal("vp", updated.Name);
        Assert.Equal("claude-opus-4", updated.Model);
        Assert.Equal("high", updated.Effort);
        Assert.Null(store.Get("coo"));
        var vp = store.Get("vp");
        Assert.NotNull(vp);
        Assert.Equal("claude-opus-4", vp.Model);
        Assert.Equal("high", vp.Effort);

        // Without this, editing name: would silently drop the teammate's Model - the exact
        // "silently resurrect an old setting" failure rules.md exists to prevent, run backwards.
        // Add can no longer write a fresh "coo" at a mismatched filename the way Phase 2b's Add
        // did (that mismatch is exactly the bug this phase fixes), and the renamed Persona's OLD
        // file ("coo.md") is still on disk holding "vp"'s content now - so the new "coo" file has
        // to land at a DIFFERENT physical path, written directly (as a hand-authored file would
        // be) and picked up through the watcher, to prove anything about the SQLite rows rather
        // than colliding on the old path.
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.PersonasChanged += () => tcs.TrySetResult();
        await File.WriteAllTextAsync(
            Path.Combine(dir.Path, "Teams", "coo-new.md"),
            PersonaText("coo", "You are a brand new Chief of Staff."),
            ct);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var registration = cts.Token.Register(() => tcs.TrySetCanceled());
        await tcs.Task;

        var recreated = store.Get("coo");
        Assert.NotNull(recreated);
        Assert.Null(recreated.Model);
        Assert.Null(recreated.Effort);
    }

    [Fact]
    public async Task Update_OnANestedPersona_WritesBackToTheNestedPath()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var businessDir = Path.Combine(dir.Path, "Teams", "Business");
        Directory.CreateDirectory(businessDir);
        var nestedPath = Path.Combine(businessDir, "coo.md");
        await File.WriteAllTextAsync(nestedPath, PersonaText("coo", "You are the Chief of Staff."), ct);
        using var store = CreateStore(dir);
        Assert.Equal("coo", Assert.Single(store.ListNames()));

        store.Update("coo", PersonaText("coo", "You are the revised Chief of Staff."), model: null, effort: null);

        Assert.Equal(PersonaText("coo", "You are the revised Chief of Staff."), await File.ReadAllTextAsync(nestedPath, ct));
        Assert.False(File.Exists(Path.Combine(dir.Path, "Teams", "coo.md")));
        Assert.Equal(nestedPath, store.PathFor("coo"));
    }

    [Fact]
    public void Remove_DeletesTheFile()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(Identity("coo"), "text");
        var path = Path.Combine(dir.Path, "Teams", "coo.md");

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
        store.Add(Identity("coo"), "text", "claude-opus-4");

        store.Remove("coo");
        var recreated = store.Add(Identity("coo"), "new text");

        Assert.Null(recreated.Model);
    }

    [Fact]
    public void Remove_AlsoRemovesTheStoredEffort()
    {
        // A stale effort must not resurrect: if it did, re-creating a Persona of the same name
        // would silently inherit an effort nobody chose for it this time.
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(Identity("coo"), "text", "claude-opus-4", "high");

        store.Remove("coo");
        var recreated = store.Add(Identity("coo"), "new text");

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
        store.Add(Identity("coo"), "text");
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
        store.Add(Identity("coo"), "text");

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
        store.Add(Identity("coo"), "text");

        var path = store.PathFor("coo");

        Assert.Equal(Path.Combine(dir.Path, "Teams", "coo.md"), path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task ExternalFileChange_IsNoticedThroughPersonasChanged()
    {
        // Proves the FileSystemWatcher (not just calls through PersonaStore itself) drives
        // PersonasChanged: this writes a .md file directly to disk, bypassing the store entirely.
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        Directory.CreateDirectory(Path.Combine(dir.Path, "Teams"));

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.PersonasChanged += () => tcs.TrySetResult();

        await File.WriteAllTextAsync(
            Path.Combine(dir.Path, "Teams", "external.md"),
            PersonaText("external", "You are External."),
            TestContext.Current.CancellationToken);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var registration = cts.Token.Register(() => tcs.TrySetCanceled());
        await tcs.Task;

        Assert.Contains("external", store.ListNames());
    }

    /// <summary>
    /// A Persona nested under a Team sub-folder is exactly as much a Persona as one at the top
    /// level of the Teams directory: Team sub-folders are purely organisational, so recursive
    /// discovery must surface it under its front-matter Name with no other change in behaviour.
    /// </summary>
    [Fact]
    public void ListNames_IncludesAPersonaNestedInATeamSubFolder()
    {
        using var dir = new TempDataDir();
        var teamsDir = Path.Combine(dir.Path, "Teams", "Business");
        Directory.CreateDirectory(teamsDir);
        File.WriteAllText(Path.Combine(teamsDir, "coo.md"), PersonaText("coo", "You are the Chief of Staff."));
        using var store = CreateStore(dir);

        var names = store.ListNames();

        Assert.Contains("coo", names);
    }

    /// <summary>
    /// Supersedes Phase 2a's ListNames_FoldsDuplicateFilenamesAcrossTeamSubFoldersToOneEntry: with
    /// real front-matter identity, two files that resolve to the same Name are a collision, not a
    /// coincidence to fold away by picking a winner. Picking a winner by enumeration order is
    /// exactly the "I edited the file and nothing happened" failure this phase's collision rules
    /// exist to prevent, so BOTH files are now rejected instead.
    /// </summary>
    [Fact]
    public void TwoPersonaFilesWithTheSameNameInDifferentTeamSubFolders_AreBothRejectedAsACollision()
    {
        using var dir = new TempDataDir();
        var teamA = Path.Combine(dir.Path, "Teams", "Business");
        var teamB = Path.Combine(dir.Path, "Teams", "Engineering");
        Directory.CreateDirectory(teamA);
        Directory.CreateDirectory(teamB);
        File.WriteAllText(Path.Combine(teamA, "coo.md"), PersonaText("coo", "Business flavour."));
        File.WriteAllText(Path.Combine(teamB, "coo.md"), PersonaText("coo", "Engineering flavour."));
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
        var teamsDir = Path.Combine(dir.Path, "Teams");
        Directory.CreateDirectory(teamsDir);
        File.WriteAllText(Path.Combine(teamsDir, "zzz.md"), PersonaText("Jarvis", "You are Jarvis."));
        using var store = CreateStore(dir);

        var names = store.ListNames();

        Assert.Contains("Jarvis", names);
        Assert.DoesNotContain("zzz", names);
    }

    /// <summary>
    /// The single most important behaviour this phase adds: Team sub-folders are organisational
    /// only, so moving a file between them must be a complete no-op - same Name, same Model, same
    /// Effort, reachable at its new path.
    /// </summary>
    [Fact]
    public async Task PersonaMovedBetweenTeamSubFolders_KeepsItsNameModelAndEffort()
    {
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        store.Add(Identity("coo"), "You are the Chief of Staff.", "claude-opus-4", "high");
        var oldPath = store.PathFor("coo");
        var before = store.Get("coo");

        var businessDir = Path.Combine(dir.Path, "Teams", "Business");
        Directory.CreateDirectory(businessDir);
        var newPath = Path.Combine(businessDir, Path.GetFileName(oldPath));

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.PersonasChanged += () => tcs.TrySetResult();

        File.Move(oldPath, newPath);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var registration = cts.Token.Register(() => tcs.TrySetCanceled());
        await tcs.Task;

        var after = store.Get("coo");
        Assert.NotNull(after);
        Assert.Equal(before!.Name, after.Name);
        Assert.Equal(before.Text, after.Text);
        Assert.Equal(before.Model, after.Model);
        Assert.Equal(before.Effort, after.Effort);
        Assert.Equal(newPath, store.PathFor("coo"));
    }

    /// <summary>The Phase 2a ghost-row bug, pinned: a Persona discovered only under a Team sub-folder must be reachable through Get and PathFor, not just listed.</summary>
    [Fact]
    public void NestedPersona_IsFullyReachableThroughGetAndPathFor()
    {
        using var dir = new TempDataDir();
        var teamsDir = Path.Combine(dir.Path, "Teams", "Business");
        Directory.CreateDirectory(teamsDir);
        var path = Path.Combine(teamsDir, "coo.md");
        File.WriteAllText(path, PersonaText("coo", "You are the Chief of Staff."));
        using var store = CreateStore(dir);

        Assert.Contains("coo", store.ListNames());
        var persona = store.Get("coo");
        Assert.NotNull(persona);
        Assert.Equal(PersonaText("coo", "You are the Chief of Staff."), persona.Text);
        Assert.Equal(path, store.PathFor("coo"));
    }

    [Fact]
    public void RejectedFiles_ReportsAMalformedFilesPathAndReason()
    {
        using var dir = new TempDataDir();
        var teamsDir = Path.Combine(dir.Path, "Teams");
        Directory.CreateDirectory(teamsDir);
        var path = Path.Combine(teamsDir, "broken.md");
        File.WriteAllText(path, "---\nTitle: Chief of Staff\nAlias: coo\n---\nYou are the Chief of Staff.");
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
        store.Add(Identity("coo", teams: ["Household", "Business"]), "body");
        store.Add(Identity("cto", teams: ["Business", "Engineering"]), "body");

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

    [Fact]
    public async Task ExternalFileChange_InATeamSubFolder_IsNoticedThroughPersonasChanged()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var teamsDir = Path.Combine(dir.Path, "Teams", "Business");
        Directory.CreateDirectory(teamsDir);
        var path = Path.Combine(teamsDir, "coo.md");
        File.WriteAllText(path, PersonaText("coo", "You are the Chief of Staff."));
        using var store = CreateStore(dir);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.PersonasChanged += () => tcs.TrySetResult();

        await File.WriteAllTextAsync(path, PersonaText("coo", "You are the Chief of Staff, revised."), ct);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var registration = cts.Token.Register(() => tcs.TrySetCanceled());
        await tcs.Task;

        Assert.Contains("coo", store.ListNames());
    }

    /// <summary>
    /// A Team sub-folder created after the store (and its watcher) already started must still be
    /// picked up: <see cref="FileSystemWatcher.IncludeSubdirectories"/> recurses into directories
    /// that appear later, not only ones present at construction time.
    /// </summary>
    [Fact]
    public async Task ExternalFileCreated_InANewlyCreatedTeamSubFolder_IsNoticedThroughPersonasChanged()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var store = CreateStore(dir);
        var newTeamDir = Path.Combine(dir.Path, "Teams", "NewTeam");

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.PersonasChanged += () => tcs.TrySetResult();

        Directory.CreateDirectory(newTeamDir);
        await File.WriteAllTextAsync(Path.Combine(newTeamDir, "cto.md"), PersonaText("cto", "You are the CTO."), ct);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var registration = cts.Token.Register(() => tcs.TrySetCanceled());
        await tcs.Task;

        Assert.Contains("cto", store.ListNames());
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
        var teamsDir = Path.Combine(dir.Path, "Teams");

        var raised = false;
        store.PersonasChanged += () => raised = true;

        await File.WriteAllTextAsync(Path.Combine(teamsDir, "notes.txt"), "not a persona", ct);
        await Task.Delay(TimeSpan.FromMilliseconds(750), ct);

        // The real assertion: a ".txt" alone, given a full debounce window and then some to
        // settle, raised nothing.
        Assert.False(raised);

        // Proves the watcher was alive and listening the whole time (rather than this test just
        // never having given it a chance to fire): a genuine Persona file written right after
        // still gets noticed.
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.PersonasChanged += () => tcs.TrySetResult();
        await File.WriteAllTextAsync(Path.Combine(teamsDir, "coo.md"), PersonaText("coo", "You are the Chief of Staff."), ct);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var registration = cts.Token.Register(() => tcs.TrySetCanceled());
        await tcs.Task;
    }

    /// <summary>
    /// The other half of widening the watcher's filter to "*": a directory rename raises a
    /// Renamed event whose Name is the directory itself, never ".md", so the handler's directory
    /// branch (rather than its markdown-extension check) is what has to catch this or every
    /// Persona path nested under the renamed folder would go stale with nothing to notice.
    /// </summary>
    [Fact]
    public async Task TeamSubFolderRename_IsNoticedThroughPersonasChanged()
    {
        using var dir = new TempDataDir();
        var oldTeamDir = Path.Combine(dir.Path, "Teams", "Business");
        Directory.CreateDirectory(oldTeamDir);
        File.WriteAllText(Path.Combine(oldTeamDir, "coo.md"), PersonaText("coo", "You are the Chief of Staff."));
        using var store = CreateStore(dir);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.PersonasChanged += () => tcs.TrySetResult();

        Directory.Move(oldTeamDir, Path.Combine(dir.Path, "Teams", "BusinessOps"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var registration = cts.Token.Register(() => tcs.TrySetCanceled());
        await tcs.Task;

        Assert.Contains("coo", store.ListNames());
    }

    /// <summary>
    /// The other half of the Deleted-event fix: removing a whole Team sub-folder outright (not
    /// renaming it away) raises a Deleted event whose Name is the directory itself, never ".md" -
    /// and unlike a rename, Directory.Exists(e.FullPath) is false by the time the handler runs, so
    /// AffectsATeamsFile must recognise the directory shape (no extension) instead. Now that the
    /// index caches each file's text, missing this would mean PersonaStore keeps serving the
    /// removed Persona's stale text forever, rather than merely a stale listing.
    /// </summary>
    [Fact]
    public async Task TeamSubFolderDeleted_IsNoticedThroughPersonasChanged()
    {
        using var dir = new TempDataDir();
        var teamDir = Path.Combine(dir.Path, "Teams", "Business");
        Directory.CreateDirectory(teamDir);
        File.WriteAllText(Path.Combine(teamDir, "coo.md"), PersonaText("coo", "You are the Chief of Staff."));
        using var store = CreateStore(dir);
        Assert.Contains("coo", store.ListNames());

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.PersonasChanged += () => tcs.TrySetResult();

        Directory.Delete(teamDir, recursive: true);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var registration = cts.Token.Register(() => tcs.TrySetCanceled());
        await tcs.Task;

        Assert.DoesNotContain("coo", store.ListNames());
    }

    private static PersonaStore CreateStore(TempDataDir dir)
    {
        return new PersonaStore(dir.Options(), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), NullLogger<PersonaStore>.Instance);
    }

    /// <summary>A valid <see cref="PersonaIdentity"/> for <paramref name="name"/>, with Title and Alias defaulting to <paramref name="name"/> and no Teams unless <paramref name="teams"/> is given - the structured input <see cref="PersonaStore.Add"/> now takes.</summary>
    private static PersonaIdentity Identity(string name, string? title = null, string? alias = null, IReadOnlyList<string>? teams = null) =>
        new(name, title ?? name, alias ?? name, teams ?? []);

    /// <summary>Minimal valid Persona frontmatter (Name, Title and Alias all <paramref name="name"/>) wrapped around <paramref name="body"/>, in the raw-text shape <see cref="PersonaStore.Update"/> and a hand-authored file both use.</summary>
    private static string PersonaText(string name, string body) => $"---\nName: {name}\nTitle: {name}\nAlias: {name}\n---\n{body}";

    /// <summary>Overload of <see cref="PersonaText(string, string)"/> that also hand-writes an <c>Adapter:</c> line, for tests pinning Spec §7.2/§7.3's Adapter round trip.</summary>
    private static string PersonaText(string name, string body, string adapter) => $"---\nName: {name}\nTitle: {name}\nAlias: {name}\nAdapter: {adapter}\n---\n{body}";
}
