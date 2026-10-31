namespace Agency.Huddle.Tests.Prompts;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Components.Settings;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Waiting;
using Agency.Huddle.Tests.Elicitation;

/// <summary>
/// Tests for <see cref="PromptStore"/>: that it joins <see cref="PromptCatalog"/>'s defaults with an
/// override file, tolerates every shape of a missing or malformed file without throwing, never
/// drops a user's unrelated data, and — the most important invariant here — rebuilds its resolved
/// snapshot before raising <see cref="PromptStore.PromptsChanged"/> rather than after.
/// </summary>
public sealed class PromptStoreTests
{
    /// <summary>Absent file: every one of the catalog's keys resolves to its own default text.</summary>
    [Fact]
    public void Raw_NoOverrideFile_EveryKeyResolvesToCatalogDefault()
    {
        using var dataDir = new TempDataDir();
        var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);

        foreach (var prompt in PromptCatalog.All)
        {
            Assert.Equal(prompt.Default, store.Raw(prompt.Key));
        }
    }

    /// <summary>A missing override file is the normal first-run case, so the constructor never creates one.</summary>
    [Fact]
    public void Constructor_NoOverrideFile_DoesNotCreateOne()
    {
        using var dataDir = new TempDataDir();
        _ = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);

        Assert.False(File.Exists(Path.Combine(dataDir.Path, "prompts.json")));
    }

    /// <summary>A file overriding one key leaves every other key resolving to its catalog default.</summary>
    [Fact]
    public void Raw_FileWithOneOverride_ThatKeyWinsAndOthersDefault()
    {
        using var dataDir = new TempDataDir();
        const string overriddenText = "[Room: {{roomName}} #{{roomId}}]";
        WritePromptsJson(dataDir.Path, new Dictionary<string, string> { ["turn.roomLabel"] = overriddenText });

        var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);

        Assert.Equal(overriddenText, store.Raw("turn.roomLabel"));

        foreach (var prompt in PromptCatalog.All.Where(h => h.Key != "turn.roomLabel"))
        {
            Assert.Equal(prompt.Default, store.Raw(prompt.Key));
        }
    }

    /// <summary>Malformed JSON falls back to every default, does not throw, and logs a warning naming the file's path.</summary>
    [Fact]
    public void Constructor_MalformedJson_FallsBackToDefaultsAndLogsWarning()
    {
        using var dataDir = new TempDataDir();
        var path = Path.Combine(dataDir.Path, "prompts.json");
        File.WriteAllText(path, "{ this is not valid json");
        var logger = new RecordingLogger<PromptStore>();

        var store = new PromptStore(dataDir.Options(), logger);

        foreach (var prompt in PromptCatalog.All)
        {
            Assert.Equal(prompt.Default, store.Raw(prompt.Key));
        }

        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains(path, warning.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A key in the file that names no prompt in the catalog is ignored for resolution, but is kept
    /// in the file — a save of some other key must not delete a user's unrelated data.
    /// </summary>
    [Fact]
    public void Save_FileHasUnknownKey_UnknownKeySurvivesAndIsIgnoredForResolution()
    {
        using var dataDir = new TempDataDir();
        WritePromptsJson(dataDir.Path, new Dictionary<string, string> { ["not.a.real.prompt"] = "keep-me" });
        var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);

        store.Save("getHelp.budget", "a brand new override");

        var onDisk = ReadPromptsJson(dataDir.Path);
        Assert.Equal("keep-me", onDisk["not.a.real.prompt"]);
        Assert.Equal("a brand new override", onDisk["getHelp.budget"]);
    }

    /// <summary>An unknown key in the file is logged (debug or information), never as a warning.</summary>
    [Fact]
    public void Constructor_FileHasUnknownKey_LogsAtInformationOrDebugNeverWarning()
    {
        using var dataDir = new TempDataDir();
        WritePromptsJson(dataDir.Path, new Dictionary<string, string> { ["not.a.real.prompt"] = "keep-me" });
        var logger = new RecordingLogger<PromptStore>();

        _ = new PromptStore(dataDir.Options(), logger);

        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains(logger.Entries, e =>
            (e.Level == LogLevel.Information || e.Level == LogLevel.Debug) &&
            e.Message.Contains("not.a.real.prompt", StringComparison.Ordinal));
    }

    /// <summary>A saved override round-trips through <see cref="PromptStore.Raw"/>.</summary>
    [Fact]
    public void Save_ThenRaw_RoundTrips()
    {
        using var dataDir = new TempDataDir();
        var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);

        store.Save("getHelp.intro", "A brand new introduction.");

        Assert.Equal("A brand new introduction.", store.Raw("getHelp.intro"));
    }

    /// <summary>Saving the catalog's own default text removes the override rather than storing a redundant copy.</summary>
    [Fact]
    public void Save_WithCatalogDefaultText_RemovesAnyExistingOverride()
    {
        using var dataDir = new TempDataDir();
        var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);
        store.Save("getHelp.intro", "A brand new introduction.");

        store.Save("getHelp.intro", PromptCatalog.Get("getHelp.intro").Default);

        var onDisk = ReadPromptsJson(dataDir.Path);
        Assert.False(onDisk.ContainsKey("getHelp.intro"));
        Assert.Equal(PromptCatalog.Get("getHelp.intro").Default, store.Raw("getHelp.intro"));
    }

    /// <summary><see cref="PromptStore.Reset"/> reverts a saved override back to the catalog default.</summary>
    [Fact]
    public void Reset_AfterSave_RevertsToCatalogDefault()
    {
        using var dataDir = new TempDataDir();
        var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);
        store.Save("getHelp.intro", "A brand new introduction.");

        store.Reset("getHelp.intro");

        Assert.Equal(PromptCatalog.Get("getHelp.intro").Default, store.Raw("getHelp.intro"));
        var onDisk = ReadPromptsJson(dataDir.Path);
        Assert.False(onDisk.ContainsKey("getHelp.intro"));
    }

    /// <summary><see cref="PromptStore.Render"/> substitutes placeholders the same way <see cref="PromptRenderer"/> does.</summary>
    [Fact]
    public void Render_SubstitutesThroughToPromptRenderer()
    {
        using var dataDir = new TempDataDir();
        var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);
        var values = new Dictionary<string, string>
        {
            ["{{roomName}}"] = "Ops",
            ["{{roomId}}"] = "42",
        };

        var rendered = store.Render("turn.roomLabel", values);

        var expected = PromptRenderer.Render(PromptCatalog.Get("turn.roomLabel").Default, values);
        Assert.Equal(expected, rendered);
    }

    /// <summary><see cref="PromptStore.Raw"/> with a key naming no prompt throws <see cref="KeyNotFoundException"/>.</summary>
    [Fact]
    public void Raw_UnknownKey_ThrowsKeyNotFoundException()
    {
        using var dataDir = new TempDataDir();
        var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);

        Assert.Throws<KeyNotFoundException>(() => store.Raw("not.a.real.prompt"));
    }

    /// <summary><see cref="PromptStore.Render"/> with a key naming no prompt throws <see cref="KeyNotFoundException"/>.</summary>
    [Fact]
    public void Render_UnknownKey_ThrowsKeyNotFoundException()
    {
        using var dataDir = new TempDataDir();
        var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);

        Assert.Throws<KeyNotFoundException>(() => store.Render("not.a.real.prompt", new Dictionary<string, string>()));
    }

    /// <summary>
    /// The rebuild-before-notify invariant: a handler subscribed to <see cref="PromptStore.PromptsChanged"/>
    /// that reads the store from inside its own callback must see the value the save just produced,
    /// never the value that was true a moment before. This mirrors the exact ordering
    /// <c>PersonaStore.OnDebounceElapsed</c> documents and depends on for the same reason.
    /// </summary>
    [Fact]
    public void PromptsChanged_HandlerReadsStoreInsideCallback_SeesTheNewValue()
    {
        using var dataDir = new TempDataDir();
        var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);
        string? seenInsideHandler = null;
        store.PromptsChanged += () => seenInsideHandler = store.Raw("getHelp.intro");

        store.Save("getHelp.intro", "Freshly saved text.");

        Assert.Equal("Freshly saved text.", seenInsideHandler);
    }

    /// <summary>
    /// <see cref="PromptStore.SaveMany"/>'s whole reason to exist: several edits applied together raise
    /// <see cref="PromptStore.PromptsChanged"/> exactly once, not once per edited key.
    /// </summary>
    [Fact]
    public void SaveMany_SeveralEdits_RaisesPromptsChangedExactlyOnce()
    {
        using var dataDir = new TempDataDir();
        var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);
        var invocationCount = 0;
        store.PromptsChanged += () => invocationCount++;

        store.SaveMany(new Dictionary<string, string>
        {
            ["getHelp.intro"] = "A brand new introduction.",
            ["getHelp.budget"] = "A brand new budget line.",
            ["turn.roomLabel"] = "[Room: {{roomName}} #{{roomId}}]",
        });

        Assert.Equal(1, invocationCount);
        Assert.Equal("A brand new introduction.", store.Raw("getHelp.intro"));
        Assert.Equal("A brand new budget line.", store.Raw("getHelp.budget"));
        Assert.Equal("[Room: {{roomName}} #{{roomId}}]", store.Raw("turn.roomLabel"));
    }

    /// <summary>Among a batch of edits, one whose text equals the catalog default removes that key's override rather than storing a redundant copy.</summary>
    [Fact]
    public void SaveMany_OneEditEqualsTheCatalogDefault_RemovesThatOverrideOnly()
    {
        using var dataDir = new TempDataDir();
        var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);
        store.Save("getHelp.intro", "A previously saved override.");

        store.SaveMany(new Dictionary<string, string>
        {
            ["getHelp.intro"] = PromptCatalog.Get("getHelp.intro").Default,
            ["getHelp.budget"] = "A brand new budget line.",
        });

        var onDisk = ReadPromptsJson(dataDir.Path);
        Assert.False(onDisk.ContainsKey("getHelp.intro"));
        Assert.Equal("A brand new budget line.", onDisk["getHelp.budget"]);
    }

    /// <summary>A batch save re-reads the file first, so a key the batch never touches survives on disk.</summary>
    [Fact]
    public void SaveMany_UnrelatedKeyOnDisk_Survives()
    {
        using var dataDir = new TempDataDir();
        WritePromptsJson(dataDir.Path, new Dictionary<string, string> { ["not.a.real.prompt"] = "keep-me" });
        var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);

        store.SaveMany(new Dictionary<string, string> { ["getHelp.intro"] = "A brand new introduction." });

        var onDisk = ReadPromptsJson(dataDir.Path);
        Assert.Equal("keep-me", onDisk["not.a.real.prompt"]);
        Assert.Equal("A brand new introduction.", onDisk["getHelp.intro"]);
    }

    /// <summary>
    /// A stored value equal to the default apart from <c>\r\n</c> line endings is not an override: the
    /// default resolves, so the Settings field shows no "Modified" badge for it (decision D6).
    /// </summary>
    [Fact]
    public void Load_OverrideEqualToDefaultButCrlf_ResolvesDefaultAndIsNotOverridden()
    {
        using var dataDir = new TempDataDir();
        PromptDefinition multiLine = MultiLinePrompt();
        WritePromptsJson(dataDir.Path, new Dictionary<string, string> { [multiLine.Key] = multiLine.Default.ReplaceLineEndings("\r\n") });

        var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);

        Assert.Equal(multiLine.Default, store.Raw(multiLine.Key));
    }

    /// <summary>
    /// After a load that ignored a CRLF-only override, the next save drops that key from the file
    /// instead of keeping the redundant copy, and keeps the edit that was saved.
    /// </summary>
    [Fact]
    public void SaveMany_AfterCrlfOverride_RemovesKeyFromFile()
    {
        using var dataDir = new TempDataDir();
        PromptDefinition multiLine = MultiLinePrompt();
        WritePromptsJson(dataDir.Path, new Dictionary<string, string> { [multiLine.Key] = multiLine.Default.ReplaceLineEndings("\r\n") });
        var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);

        store.SaveMany(new Dictionary<string, string> { ["getHelp.intro"] = "A brand new introduction." });

        var onDisk = ReadPromptsJson(dataDir.Path);
        Assert.False(onDisk.ContainsKey(multiLine.Key));
        Assert.Equal("A brand new introduction.", onDisk["getHelp.intro"]);
    }

    /// <summary>
    /// Resetting an unrelated key rewrites the file without a CRLF-only override, the same as a save does.
    /// </summary>
    [Fact]
    public void Reset_OfAnotherKey_RemovesCrlfOnlyOverrideFromFile()
    {
        using var dataDir = new TempDataDir();
        PromptDefinition multiLine = MultiLinePrompt();
        WritePromptsJson(dataDir.Path, new Dictionary<string, string> { [multiLine.Key] = multiLine.Default.ReplaceLineEndings("\r\n") });
        var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);

        store.Reset("getHelp.intro");

        Assert.False(ReadPromptsJson(dataDir.Path).ContainsKey(multiLine.Key));
    }

    /// <summary>An empty edit set is a no-op: no file write and no <see cref="PromptStore.PromptsChanged"/>.</summary>
    [Fact]
    public void SaveMany_EmptyEditSet_RaisesNothingAndWritesNoFile()
    {
        using var dataDir = new TempDataDir();
        var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);
        var invocationCount = 0;
        store.PromptsChanged += () => invocationCount++;

        store.SaveMany(new Dictionary<string, string>());

        Assert.Equal(0, invocationCount);
        Assert.False(File.Exists(Path.Combine(dataDir.Path, "prompts.json")));
    }

    /// <summary>
    /// The full "Reset all to defaults" flow end to end: staging every default via
    /// <see cref="PromptFieldFactory.StageAllDefaults"/> touches only the in-memory pending-edits map,
    /// leaving the override file exactly as it was until a caller goes on to call
    /// <see cref="PromptStore.SaveMany"/>, at which point every previously overridden key reverts.
    /// </summary>
    [Fact]
    public void ResetAllFlow_StageDefaultsThenSaveMany_LeavesFileUntouchedUntilSaveAndThenRestoresEveryKey()
    {
        using var dataDir = new TempDataDir();
        var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);
        store.Save("getHelp.intro", "A previously saved override.");
        store.Save("turn.roomLabel", "[Room: {{roomName}} #{{roomId}}]");

        var pendingEdits = new Dictionary<string, string>(StringComparer.Ordinal);
        PromptFieldFactory.StageAllDefaults(pendingEdits);

        // Staging alone must not have touched the file: both overrides saved above are still there.
        var onDiskBeforeSave = ReadPromptsJson(dataDir.Path);
        Assert.True(onDiskBeforeSave.ContainsKey("getHelp.intro"));
        Assert.True(onDiskBeforeSave.ContainsKey("turn.roomLabel"));

        store.SaveMany(pendingEdits);

        foreach (var prompt in PromptCatalog.All)
        {
            Assert.Equal(prompt.Default, store.Raw(prompt.Key));
        }

        var onDiskAfterSave = ReadPromptsJson(dataDir.Path);
        Assert.False(onDiskAfterSave.ContainsKey("getHelp.intro"));
        Assert.False(onDiskAfterSave.ContainsKey("turn.roomLabel"));
    }

    /// <summary><see cref="PromptStore.FilePath"/> reports the exact override-file path this instance was constructed with, whether or not the file exists yet.</summary>
    [Fact]
    public void FilePath_ReportsTheOverrideFilePath()
    {
        using var dataDir = new TempDataDir();
        var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);

        Assert.Equal(Path.Combine(dataDir.Path, "prompts.json"), store.FilePath);
        Assert.False(File.Exists(store.FilePath));

        store.Save("getHelp.intro", "Freshly saved text.");

        Assert.True(File.Exists(store.FilePath));
    }

    /// <summary>The same rebuild-before-notify invariant, exercised through <see cref="PromptStore.Reset"/> instead of <see cref="PromptStore.Save"/>.</summary>
    [Fact]
    public void PromptsChanged_AfterReset_HandlerReadsStoreInsideCallback_SeesTheDefault()
    {
        using var dataDir = new TempDataDir();
        var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);
        store.Save("getHelp.intro", "Freshly saved text.");
        string? seenInsideHandler = null;
        store.PromptsChanged += () => seenInsideHandler = store.Raw("getHelp.intro");

        store.Reset("getHelp.intro");

        Assert.Equal(PromptCatalog.Get("getHelp.intro").Default, seenInsideHandler);
    }

    /// <summary>An external write to <c>prompts.json</c> (no <see cref="PromptStore.Save"/> call) is picked up and resolves through <see cref="PromptStore.Raw"/>.</summary>
    [Fact]
    public async Task Raw_ExternalWriteToOverrideFile_PicksUpNewValue()
    {
        using var dataDir = new TempDataDir();
        using var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);

        WritePromptsJson(dataDir.Path, new Dictionary<string, string> { ["getHelp.intro"] = "Externally edited text." });

        await Eventually.UntilAsync(() => string.Equals(store.Raw("getHelp.intro"), "Externally edited text.", StringComparison.Ordinal), TestContext.Current.CancellationToken, timeout: TestBudgets.Slow, interval: TimeSpan.FromMilliseconds(50));

        Assert.Equal("Externally edited text.", store.Raw("getHelp.intro"));
    }

    /// <summary>An external edit to <c>prompts.json</c> raises <see cref="PromptStore.PromptsChanged"/> once the watcher's debounce settles.</summary>
    [Fact]
    public async Task PromptsChanged_ExternalEdit_IsRaised()
    {
        using var dataDir = new TempDataDir();
        using var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);
        var raised = false;
        store.PromptsChanged += () => raised = true;

        WritePromptsJson(dataDir.Path, new Dictionary<string, string> { ["getHelp.intro"] = "Externally edited text." });

        await Eventually.UntilAsync(() => raised, TestContext.Current.CancellationToken, timeout: TestBudgets.Slow, interval: TimeSpan.FromMilliseconds(50));

        Assert.True(raised);
    }

    /// <summary>Deleting the override file reverts every key to its catalog default - "no overrides" is a legitimate, not an error, state.</summary>
    [Fact]
    public async Task Raw_OverrideFileDeleted_EveryKeyRevertsToCatalogDefault()
    {
        using var dataDir = new TempDataDir();
        using var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);
        store.Save("getHelp.intro", "A previously saved override.");
        Assert.Equal("A previously saved override.", store.Raw("getHelp.intro"));

        File.Delete(Path.Combine(dataDir.Path, "prompts.json"));

        await Eventually.UntilAsync(() => string.Equals(store.Raw("getHelp.intro"), PromptCatalog.Get("getHelp.intro").Default, StringComparison.Ordinal), TestContext.Current.CancellationToken, timeout: TestBudgets.Slow, interval: TimeSpan.FromMilliseconds(50));

        foreach (var prompt in PromptCatalog.All)
        {
            Assert.Equal(prompt.Default, store.Raw(prompt.Key));
        }
    }

    /// <summary>
    /// A rename-over save - writing a temp file, then <see cref="File.Move(string, string, bool)"/>'ing
    /// it over <c>prompts.json</c>, the idiom many editors and <see cref="File.Replace(string, string, string?)"/>
    /// use - arrives as a <see cref="WatcherChangeTypes.Renamed"/> event, not <see cref="WatcherChangeTypes.Changed"/>,
    /// and must still be picked up. A watcher that only subscribed to <c>Changed</c> would miss this.
    /// </summary>
    [Fact]
    public async Task Raw_RenameOverSave_IsPickedUp()
    {
        using var dataDir = new TempDataDir();
        using var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);
        var targetPath = Path.Combine(dataDir.Path, "prompts.json");
        var tempPath = Path.Combine(dataDir.Path, "prompts.json.tmp");

        File.WriteAllText(tempPath, JsonSerializer.Serialize(
            new Dictionary<string, string> { ["getHelp.intro"] = "Renamed into place." },
            ProtocolJson.Options));
        File.Move(tempPath, targetPath, overwrite: true);

        await Eventually.UntilAsync(() => string.Equals(store.Raw("getHelp.intro"), "Renamed into place.", StringComparison.Ordinal), TestContext.Current.CancellationToken, timeout: TestBudgets.Slow, interval: TimeSpan.FromMilliseconds(50));

        Assert.Equal("Renamed into place.", store.Raw("getHelp.intro"));
    }

    /// <summary>Disposing stops the watcher: an edit made after <see cref="PromptStore.Dispose"/> raises nothing and is not picked up.</summary>
    [Fact]
    public async Task Dispose_StopsWatcher_NoFurtherRaisesOrPickups()
    {
        using var dataDir = new TempDataDir();
        var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);
        var raised = false;
        store.PromptsChanged += () => raised = true;

        store.Dispose();

        WritePromptsJson(dataDir.Path, new Dictionary<string, string> { ["getHelp.intro"] = "Written after dispose." });

        // No event to wait for a positive signal on, so this waits out a window comfortably longer
        // than the debounce and the retry budget, then asserts nothing happened - the only way to
        // assert an absence for a timing-dependent watcher.
        await Task.Delay(TimeSpan.FromMilliseconds(800), TestContext.Current.CancellationToken);

        Assert.False(raised);
        Assert.Equal(PromptCatalog.Get("getHelp.intro").Default, store.Raw("getHelp.intro"));
    }

    /// <summary>A rapid burst of external writes collapses to fewer <see cref="PromptStore.PromptsChanged"/> raises than writes, via the debounce.</summary>
    [Fact]
    public async Task PromptsChanged_RapidBurstOfWrites_CollapsesToFewerRaisesThanWrites()
    {
        using var dataDir = new TempDataDir();
        using var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);
        var invocationCount = 0;
        store.PromptsChanged += () => Interlocked.Increment(ref invocationCount);
        const int writeCount = 10;

        for (var i = 0; i < writeCount; i++)
        {
            WritePromptsJson(dataDir.Path, new Dictionary<string, string> { ["getHelp.intro"] = $"Burst write {i}." });
        }

        // The last write must have settled and been observed before asserting the raise count, or
        // this could read a count captured mid-burst.
        await Eventually.UntilAsync(() => string.Equals(store.Raw("getHelp.intro"), $"Burst write {writeCount - 1}.", StringComparison.Ordinal), TestContext.Current.CancellationToken, timeout: TestBudgets.Slow, interval: TimeSpan.FromMilliseconds(50));

        // A generous upper bound rather than an exact count: exactly how many debounce windows a
        // tight burst on this machine's filesystem collapses into is itself timing-dependent, and
        // asserting an exact number would be its own flake. What matters is that it collapsed at
        // all - far fewer raises than the ten writes above.
        Assert.True(invocationCount < writeCount);
    }

    /// <summary>The first catalog prompt whose default spans several lines, so a CRLF copy of it differs from the default byte for byte.</summary>
    private static PromptDefinition MultiLinePrompt() =>
        PromptCatalog.All.First(p => p.Default.Contains('\n') && !string.Equals(p.Key, "getHelp.intro", StringComparison.Ordinal));

    /// <summary>Writes a flat override JSON object directly, as a human editing the file by hand would.</summary>
    /// <param name="dataDir">The temp data directory a <see cref="PromptStore"/> will be pointed at.</param>
    /// <param name="overrides">The override keys and text to write.</param>
    private static void WritePromptsJson(string dataDir, Dictionary<string, string> overrides)
    {
        var path = Path.Combine(dataDir, "prompts.json");
        File.WriteAllText(path, JsonSerializer.Serialize(overrides, ProtocolJson.Options));
    }

    /// <summary>Reads the override file back as a flat dictionary, for asserting what a <see cref="PromptStore"/> actually wrote.</summary>
    /// <param name="dataDir">The temp data directory a <see cref="PromptStore"/> was pointed at.</param>
    private static Dictionary<string, string> ReadPromptsJson(string dataDir)
    {
        var path = Path.Combine(dataDir, "prompts.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, ProtocolJson.Options)
            ?? throw new InvalidOperationException("prompts.json deserialised to null.");
    }

    /// <summary>
    /// A watcher rebuild that cannot read <c>prompts.json</c> on any attempt (an editor holds it exclusively) logs a
    /// warning and keeps the previously resolved prompt text instead of letting the <see cref="IOException"/> escape the timer thread.
    /// </summary>
    [Fact]
    public async Task Raw_FileLockedForEveryRetry_KeepsThePreviousText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        Elicitation.RecordingLogger<PromptStore> logger = new();
        using PromptStore store = new(dataDir.Options(), logger);
        store.Save("getHelp.intro", "Saved before the lock.");
        string path = Path.Combine(dataDir.Path, "prompts.json");

        using (FileStream held = LockedWatcherFileSupport.WriteAndHold(path, """{ "getHelp.intro": "Written while locked." }"""))
        {
            await LockedWatcherFileSupport.WaitForWarningAsync(logger, ct);

            Assert.Equal("Saved before the lock.", store.Raw("getHelp.intro"));
        }
    }
}
