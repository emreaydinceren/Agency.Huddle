using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Library;
using Agency.Huddle.Tests.Elicitation;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Tests for <see cref="LibraryRootStore"/>: the Teams/Agents built-ins plus configured or
/// saved pinned roots (Spec §6.10), slug generation, hide/show of a built-in, and the
/// <c>ViewStore</c>-style malformed-file behaviour (corrections-B3 4.1.t item 1): a broken
/// <c>library-roots.json</c> keeps the configured roots, sets a load error, and refuses writes.
/// </summary>
public sealed class LibraryRootStoreTests
{
    /// <summary>With no configuration and no saved file, the two built-in roots come first, in order.</summary>
    [Fact]
    public void Roots_Default_AreTeamsThenAgents()
    {
        using var dataDir = new TempDataDir();
        var paths = new AgentPaths(dataDir.Options());
        var store = new LibraryRootStore(dataDir.Options(), paths, NullLogger<LibraryRootStore>.Instance);

        Assert.Equal("teams", store.Roots[0].Id);
        Assert.Equal(LibraryRootKind.Teams, store.Roots[0].Kind);
        Assert.Equal(Path.Combine(dataDir.Path, "Teams"), store.Roots[0].FullPath);
        Assert.Equal("Teams", store.Roots[0].DisplayName);

        Assert.Equal("agents", store.Roots[1].Id);
        Assert.Equal(LibraryRootKind.Agents, store.Roots[1].Kind);
        Assert.Equal(Path.Combine(dataDir.Path, "Agents"), store.Roots[1].FullPath);
        Assert.Equal("Agents", store.Roots[1].DisplayName);
    }

    /// <summary>The trust warning every pinning surface shows is one constant with the Spec's settled text (UX-R15).</summary>
    [Fact]
    public void TrustWarning_IsTheSettledText()
    {
        Assert.Equal("Anything in this folder can be read and changed from Huddle.", LibraryRootStore.TrustWarning);
    }

    /// <summary><c>LibraryRootStore.TryPin</c> refuses a path that is not an existing folder with the settled text, saving nothing (UX-R15).</summary>
    [Fact]
    public void TryPin_MissingFolder_ReturnsTheErrorAndSavesNothing()
    {
        using var dataDir = new TempDataDir();
        var store = new LibraryRootStore(dataDir.Options(), new AgentPaths(dataDir.Options()), NullLogger<LibraryRootStore>.Instance);

        string? error = store.TryPin("Docs", Path.Combine(dataDir.Path, "Nope"));

        Assert.Equal("That folder doesn't exist.", error);
        Assert.Empty(store.Pinned);
        Assert.False(File.Exists(store.FilePath));
    }

    /// <summary><c>LibraryRootStore.TryPin</c> pins an existing folder, returns no error, and keeps the pinned roots that were already there (UX-R15).</summary>
    [Fact]
    public void TryPin_ExistingFolder_PinsItAfterTheOthers()
    {
        using var dataDir = new TempDataDir();
        var store = new LibraryRootStore(dataDir.Options(), new AgentPaths(dataDir.Options()), NullLogger<LibraryRootStore>.Instance);
        string first = Path.Combine(dataDir.Path, "First");
        string second = Path.Combine(dataDir.Path, "Second");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);

        string? firstError = store.TryPin("First", first);
        string? secondError = store.TryPin("Second", second);

        Assert.Null(firstError);
        Assert.Null(secondError);
        Assert.Equal(["First", "Second"], store.Pinned.Select(p => p.Name));
        Assert.True(File.Exists(store.FilePath));
    }

    /// <summary>A pinned root from <c>Team:Library:Roots</c> follows the two built-ins, with an id slugged from its name.</summary>
    [Fact]
    public void Roots_ConfiguredPinned_FollowBuiltIns()
    {
        using var dataDir = new TempDataDir();
        var docsPath = Path.Combine(dataDir.Path, "Docs");
        Directory.CreateDirectory(docsPath);
        var paths = new AgentPaths(dataDir.Options());
        var options = dataDir.Options(opts => opts.Library = new LibraryOptions { Roots = [new PinnedRootOption { Name = "Huddle docs", Path = docsPath }] });

        var store = new LibraryRootStore(options, paths, NullLogger<LibraryRootStore>.Instance);

        var pinned = store.Roots[7];
        Assert.Equal("huddle-docs", pinned.Id);
        Assert.Equal(LibraryRootKind.Pinned, pinned.Kind);
        Assert.Equal(Path.GetFullPath(docsPath), pinned.FullPath);
    }

    /// <summary>A pinned path given relative to DataDir resolves under it (corrections-B3 item 3).</summary>
    [Fact]
    public void Roots_RelativePinnedPath_IsUnderDataDir()
    {
        using var dataDir = new TempDataDir();
        Directory.CreateDirectory(Path.Combine(dataDir.Path, "Docs"));
        var paths = new AgentPaths(dataDir.Options());
        var options = dataDir.Options(opts => opts.Library = new LibraryOptions { Roots = [new PinnedRootOption { Name = "Docs", Path = "Docs" }] });

        var store = new LibraryRootStore(options, paths, NullLogger<LibraryRootStore>.Instance);

        Assert.Equal(Path.GetFullPath("Docs", dataDir.Path), store.Roots.Single(root => root.Kind == LibraryRootKind.Pinned).FullPath);
    }

    /// <summary>A trailing directory separator on a pinned path is trimmed from the resolved root (corrections-B3 item 3).</summary>
    [Fact]
    public void Roots_TrailingSeparator_Trimmed()
    {
        using var dataDir = new TempDataDir();
        var docsPath = Path.Combine(dataDir.Path, "Docs");
        Directory.CreateDirectory(docsPath);
        var paths = new AgentPaths(dataDir.Options());
        var options = dataDir.Options(opts => opts.Library = new LibraryOptions { Roots = [new PinnedRootOption { Name = "Docs", Path = docsPath + Path.DirectorySeparatorChar }] });

        var store = new LibraryRootStore(options, paths, NullLogger<LibraryRootStore>.Instance);

        Assert.Equal(Path.GetFullPath(docsPath), store.Roots.Single(root => root.Kind == LibraryRootKind.Pinned).FullPath);
    }

    /// <summary>A blank pinned path is skipped rather than crashing startup, and a warning is logged (corrections-B3 item 4).</summary>
    [Fact]
    public void Roots_BlankPinnedPath_SkippedAndLogged()
    {
        using var dataDir = new TempDataDir();
        var paths = new AgentPaths(dataDir.Options());
        var options = dataDir.Options(opts => opts.Library = new LibraryOptions { Roots = [new PinnedRootOption { Name = "Blank", Path = "   " }] });
        var logger = new RecordingLogger<LibraryRootStore>();

        var store = new LibraryRootStore(options, paths, logger);

        Assert.DoesNotContain(store.Roots, r => r.DisplayName == "Blank");
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    /// <summary>A pinned root whose folder no longer exists stays listed, greyed by the caller (Spec E-1, corrections-B3 item 5).</summary>
    [Fact]
    public void Roots_MissingFolder_StillListed()
    {
        using var dataDir = new TempDataDir();
        var missingPath = Path.Combine(dataDir.Path, "Gone");
        var paths = new AgentPaths(dataDir.Options());
        var options = dataDir.Options(opts => opts.Library = new LibraryOptions { Roots = [new PinnedRootOption { Name = "Gone", Path = missingPath }] });

        var store = new LibraryRootStore(options, paths, NullLogger<LibraryRootStore>.Instance);

        Assert.Contains(store.Roots, r => r.DisplayName == "Gone" && r.FullPath == Path.GetFullPath(missingPath));
    }

    /// <summary>Slugging rules: FormD normalisation, non-alphanumerics collapsed to one dash, an empty result becomes "root" (corrections-B3 item 6).</summary>
    [Theory]
    [InlineData("Huddle docs", "huddle-docs")]
    [InlineData("  A  B ", "a-b")]
    [InlineData("Specs & Notes", "specs-notes")]
    [InlineData("!!!", "root")]
    [InlineData("Café", "cafe")]
    [InlineData("日本", "root")]
    public void Slug_Rules(string name, string expected)
    {
        Assert.Equal(expected, LibraryRootStore.Slug(name, []));
    }

    /// <summary>"teams" and "agents" are always taken inside Slug, so a name that slugs to either gets a numeric suffix, and a repeat of the same name keeps incrementing (corrections-B3 item 6).</summary>
    [Fact]
    public void Slug_ReservedName_CollisionsIncrementSuffix()
    {
        List<string> taken = [];
        var first = LibraryRootStore.Slug("Teams", taken);
        taken.Add(first);
        var second = LibraryRootStore.Slug("Teams", taken);

        Assert.Equal("teams-2", first);
        Assert.Equal("teams-3", second);
    }

    /// <summary>When a base slug is already taken by an earlier entry, the next colliding name skips to the next free suffix (corrections-B3 item 6).</summary>
    [Fact]
    public void Slug_BaseAlreadyPresent_NextNameSkipsToNextFreeSuffix()
    {
        List<string> taken = [];
        var first = LibraryRootStore.Slug("Teams 2", taken);
        taken.Add(first);
        var second = LibraryRootStore.Slug("Teams", taken);

        Assert.Equal("teams-2", first);
        Assert.Equal("teams-3", second);
    }

    /// <summary><see cref="LibraryRootStore.Save"/> writes library-roots.json, and a fresh store reads it instead of the configured list.</summary>
    [Fact]
    public void Save_CreatesFileAndReplacesConfigured()
    {
        using var dataDir = new TempDataDir();
        var paths = new AgentPaths(dataDir.Options());
        var options = dataDir.Options(opts => opts.Library = new LibraryOptions { Roots = [new PinnedRootOption { Name = "Old", Path = dataDir.Path }] });
        var store = new LibraryRootStore(options, paths, NullLogger<LibraryRootStore>.Instance);

        store.Save([new PinnedRootEntry("New", dataDir.Path)]);

        Assert.True(File.Exists(Path.Combine(dataDir.Path, "library-roots.json")));
        var reloaded = new LibraryRootStore(options, paths, NullLogger<LibraryRootStore>.Instance);
        Assert.Contains(reloaded.Roots, r => r.DisplayName == "New");
        Assert.DoesNotContain(reloaded.Roots, r => r.DisplayName == "Old");
    }

    /// <summary>Constructing the store and reading its roots never creates library-roots.json (an absent file is the normal first-run case).</summary>
    [Fact]
    public void NoFile_NoWrite()
    {
        using var dataDir = new TempDataDir();
        var paths = new AgentPaths(dataDir.Options());
        var store = new LibraryRootStore(dataDir.Options(), paths, NullLogger<LibraryRootStore>.Instance);

        _ = store.Roots;

        Assert.False(File.Exists(Path.Combine(dataDir.Path, "library-roots.json")));
    }

    /// <summary><see cref="LibraryRootStore.Reset"/> deletes the saved file and restores the configured pinned roots.</summary>
    [Fact]
    public void Reset_DeletesFile_RestoresConfigured()
    {
        using var dataDir = new TempDataDir();
        var paths = new AgentPaths(dataDir.Options());
        var options = dataDir.Options(opts => opts.Library = new LibraryOptions { Roots = [new PinnedRootOption { Name = "Configured", Path = dataDir.Path }] });
        var store = new LibraryRootStore(options, paths, NullLogger<LibraryRootStore>.Instance);
        store.Save([new PinnedRootEntry("Saved", dataDir.Path)]);

        store.Reset();

        Assert.False(File.Exists(Path.Combine(dataDir.Path, "library-roots.json")));
        Assert.Contains(store.Roots, r => r.DisplayName == "Configured");
    }

    /// <summary>The Raw root is a built-in after Teams and Agents, kind Raw, at the configured folder under DataDir, and the folder is not created.</summary>
    [Fact]
    public void Roots_Default_IncludeRawAfterAgents()
    {
        using var dataDir = new TempDataDir();
        var paths = new AgentPaths(dataDir.Options());
        var store = new LibraryRootStore(dataDir.Options(), paths, NullLogger<LibraryRootStore>.Instance);

        LibraryRoot raw = store.Roots[2];

        Assert.Equal("raw", raw.Id);
        Assert.Equal("Raw", raw.DisplayName);
        Assert.Equal(LibraryRootKind.Raw, raw.Kind);
        Assert.Equal(Path.Combine(dataDir.Path, "Raw"), raw.FullPath);
        Assert.False(Directory.Exists(raw.FullPath));
    }

    /// <summary>A configured <c>Team:Library:Raw:Dir</c> moves the Raw root.</summary>
    [Fact]
    public void Roots_ConfiguredUploadsDir_MovesTheRoot()
    {
        using var dataDir = new TempDataDir();
        var paths = new AgentPaths(dataDir.Options());
        var options = dataDir.Options(opts => opts.Library = new LibraryOptions { Raw = new LibraryRawOptions { Dir = "Attachments" } });

        var store = new LibraryRootStore(options, paths, NullLogger<LibraryRootStore>.Instance);

        Assert.Equal(Path.Combine(dataDir.Path, "Attachments"), store.Roots[2].FullPath);
    }

    /// <summary>A pinned root named "Raw" cannot take the reserved id, so it becomes <c>raw-2</c>.</summary>
    [Fact]
    public void Slug_Raw_IsReserved()
    {
        Assert.Equal("raw-2", LibraryRootStore.Slug("Raw", []));
    }

    /// <summary>Hiding the Raw root omits it from VisibleRoots, keeps it in Roots, and does not throw.</summary>
    [Fact]
    public void SetHidden_Raw_HidesTheRoot()
    {
        using var dataDir = new TempDataDir();
        var paths = new AgentPaths(dataDir.Options());
        var store = new LibraryRootStore(dataDir.Options(), paths, NullLogger<LibraryRootStore>.Instance);

        store.SetHidden("raw", true);

        Assert.DoesNotContain(store.VisibleRoots, r => r.Id == "raw");
        Assert.Contains(store.Roots, r => r.Id == "raw");
    }

    /// <summary><see cref="LibraryRootStore.Reset"/> also clears any hidden built-in (corrections-B3 item 8).</summary>
    [Fact]
    public void Reset_ClearsHiddenToo()
    {
        using var dataDir = new TempDataDir();
        var paths = new AgentPaths(dataDir.Options());
        var store = new LibraryRootStore(dataDir.Options(), paths, NullLogger<LibraryRootStore>.Instance);
        store.SetHidden("teams", true);

        store.Reset();

        Assert.Contains(store.VisibleRoots, r => r.Id == "teams");
    }

    /// <summary>Hiding a built-in root omits it from VisibleRoots but keeps it in Roots, still resolvable.</summary>
    [Fact]
    public void HiddenBuiltIn_IsOmittedFromVisibleRoots_ButStillResolvable()
    {
        using var dataDir = new TempDataDir();
        var paths = new AgentPaths(dataDir.Options());
        var store = new LibraryRootStore(dataDir.Options(), paths, NullLogger<LibraryRootStore>.Instance);

        store.SetHidden("teams", true);

        Assert.DoesNotContain(store.VisibleRoots, r => r.Id == "teams");
        Assert.Contains(store.Roots, r => r.Id == "teams");
    }

    /// <summary>Hiding a built-in with no saved file yet writes only "hidden": the pinned list still comes from configuration (corrections-B3 item 7).</summary>
    [Fact]
    public void SetHidden_NoFile_WritesOnlyHidden_PinnedStillFromConfiguration()
    {
        using var dataDir = new TempDataDir();
        var paths = new AgentPaths(dataDir.Options());
        var options = dataDir.Options(opts => opts.Library = new LibraryOptions { Roots = [new PinnedRootOption { Name = "Docs", Path = dataDir.Path }] });
        var store = new LibraryRootStore(options, paths, NullLogger<LibraryRootStore>.Instance);

        store.SetHidden("teams", true);

        var filePath = Path.Combine(dataDir.Path, "library-roots.json");
        Assert.True(File.Exists(filePath));
        JsonNode? node = JsonNode.Parse(File.ReadAllText(filePath));
        Assert.NotNull(node);
        JsonObject json = node.AsObject();
        Assert.False(json.ContainsKey("pinned"));
        Assert.True(json.ContainsKey("hidden"));

        var reloaded = new LibraryRootStore(options, paths, NullLogger<LibraryRootStore>.Instance);
        Assert.Contains(reloaded.Roots, r => r.DisplayName == "Docs");
        Assert.DoesNotContain(reloaded.VisibleRoots, r => r.Id == "teams");
    }

    /// <summary>Hiding a pinned (non-built-in) root's id is rejected: only "teams" and "agents" can be hidden (corrections-B3 item 8).</summary>
    [Fact]
    public void SetHidden_PinnedId_ThrowsArgumentException()
    {
        using var dataDir = new TempDataDir();
        var paths = new AgentPaths(dataDir.Options());
        var options = dataDir.Options(opts => opts.Library = new LibraryOptions { Roots = [new PinnedRootOption { Name = "Docs", Path = dataDir.Path }] });
        var store = new LibraryRootStore(options, paths, NullLogger<LibraryRootStore>.Instance);
        var pinnedId = Assert.Single(store.Roots, r => r.Kind == LibraryRootKind.Pinned).Id;

        Assert.Throws<ArgumentException>(() => store.SetHidden(pinnedId, true));
    }

    /// <summary><see cref="LibraryRootStore.Save"/> raises RootsChanged exactly once (corrections-B3 item 9).</summary>
    [Fact]
    public void Save_RaisesRootsChangedOnce()
    {
        using var dataDir = new TempDataDir();
        var paths = new AgentPaths(dataDir.Options());
        var store = new LibraryRootStore(dataDir.Options(), paths, NullLogger<LibraryRootStore>.Instance);
        var raised = 0;
        store.RootsChanged += () => raised++;

        store.Save([]);

        Assert.Equal(1, raised);
    }

    /// <summary>A malformed library-roots.json keeps the configured roots and sets a load error, rather than crashing or silently emptying (corrections-B3 item 1).</summary>
    [Fact]
    public void MalformedFile_ConfiguredRootsKeptAndLoadErrorSet()
    {
        using var dataDir = new TempDataDir();
        File.WriteAllText(Path.Combine(dataDir.Path, "library-roots.json"), "{ this is not valid json");
        var paths = new AgentPaths(dataDir.Options());
        var options = dataDir.Options(opts => opts.Library = new LibraryOptions { Roots = [new PinnedRootOption { Name = "Docs", Path = dataDir.Path }] });

        var store = new LibraryRootStore(options, paths, NullLogger<LibraryRootStore>.Instance);

        Assert.NotNull(store.LoadError);
        Assert.Contains(store.Roots, r => r.DisplayName == "Docs");
    }

    /// <summary>A save while the file is malformed is refused, so it can never overwrite the human's roots with a smaller set (corrections-B3 item 1).</summary>
    [Fact]
    public void MalformedFile_SaveThrows()
    {
        using var dataDir = new TempDataDir();
        File.WriteAllText(Path.Combine(dataDir.Path, "library-roots.json"), "{ this is not valid json");
        var paths = new AgentPaths(dataDir.Options());
        var store = new LibraryRootStore(dataDir.Options(), paths, NullLogger<LibraryRootStore>.Instance);

        Assert.Throws<InvalidOperationException>(() => store.Save([new PinnedRootEntry("New", dataDir.Path)]));
    }
}
