using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Tests for <see cref="LibraryRootStore"/>: the Teams/Teammates built-ins plus configured or
/// saved pinned roots (Spec §6.10), slug generation, hide/show of a built-in, and the
/// <c>ViewStore</c>-style malformed-file behaviour (corrections-B3 4.1.t item 1): a broken
/// <c>library-roots.json</c> keeps the configured roots, sets a load error, and refuses writes.
/// </summary>
public sealed class LibraryRootStoreTests
{
    /// <summary>With no configuration and no saved file, the two built-in roots come first, in order.</summary>
    [Fact]
    public void Roots_Default_AreTeamsThenTeammates()
    {
        using var dataDir = new TempDataDir();
        var paths = new TeammatePaths(dataDir.Options());
        var store = new LibraryRootStore(dataDir.Options(), paths, NullLogger<LibraryRootStore>.Instance);

        Assert.Equal("teams", store.Roots[0].Id);
        Assert.Equal(LibraryRootKind.Teams, store.Roots[0].Kind);
        Assert.Equal(Path.Combine(dataDir.Path, "Teams"), store.Roots[0].FullPath);
        Assert.Equal("Teams", store.Roots[0].DisplayName);

        Assert.Equal("teammates", store.Roots[1].Id);
        Assert.Equal(LibraryRootKind.Teammates, store.Roots[1].Kind);
        Assert.Equal(Path.Combine(dataDir.Path, "Teammates"), store.Roots[1].FullPath);
        Assert.Equal("Teammates", store.Roots[1].DisplayName);
    }

    /// <summary>A pinned root from <c>Team:Library:Roots</c> follows the two built-ins, with an id slugged from its name.</summary>
    [Fact]
    public void Roots_ConfiguredPinned_FollowBuiltIns()
    {
        using var dataDir = new TempDataDir();
        var docsPath = Path.Combine(dataDir.Path, "Docs");
        Directory.CreateDirectory(docsPath);
        var paths = new TeammatePaths(dataDir.Options());
        var options = Options.Create(new TeamOptions
        {
            DataDir = dataDir.Path,
            Library = new LibraryOptions { Roots = [new PinnedRootOption { Name = "Huddle docs", Path = docsPath }] },
        });

        var store = new LibraryRootStore(options, paths, NullLogger<LibraryRootStore>.Instance);

        var pinned = store.Roots[2];
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
        var paths = new TeammatePaths(dataDir.Options());
        var options = Options.Create(new TeamOptions
        {
            DataDir = dataDir.Path,
            Library = new LibraryOptions { Roots = [new PinnedRootOption { Name = "Docs", Path = "Docs" }] },
        });

        var store = new LibraryRootStore(options, paths, NullLogger<LibraryRootStore>.Instance);

        Assert.Equal(Path.GetFullPath("Docs", dataDir.Path), store.Roots[2].FullPath);
    }

    /// <summary>A trailing directory separator on a pinned path is trimmed from the resolved root (corrections-B3 item 3).</summary>
    [Fact]
    public void Roots_TrailingSeparator_Trimmed()
    {
        using var dataDir = new TempDataDir();
        var docsPath = Path.Combine(dataDir.Path, "Docs");
        Directory.CreateDirectory(docsPath);
        var paths = new TeammatePaths(dataDir.Options());
        var options = Options.Create(new TeamOptions
        {
            DataDir = dataDir.Path,
            Library = new LibraryOptions { Roots = [new PinnedRootOption { Name = "Docs", Path = docsPath + Path.DirectorySeparatorChar }] },
        });

        var store = new LibraryRootStore(options, paths, NullLogger<LibraryRootStore>.Instance);

        Assert.Equal(Path.GetFullPath(docsPath), store.Roots[2].FullPath);
    }

    /// <summary>A blank pinned path is skipped rather than crashing startup, and a warning is logged (corrections-B3 item 4).</summary>
    [Fact]
    public void Roots_BlankPinnedPath_SkippedAndLogged()
    {
        using var dataDir = new TempDataDir();
        var paths = new TeammatePaths(dataDir.Options());
        var options = Options.Create(new TeamOptions
        {
            DataDir = dataDir.Path,
            Library = new LibraryOptions { Roots = [new PinnedRootOption { Name = "Blank", Path = "   " }] },
        });
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
        var paths = new TeammatePaths(dataDir.Options());
        var options = Options.Create(new TeamOptions
        {
            DataDir = dataDir.Path,
            Library = new LibraryOptions { Roots = [new PinnedRootOption { Name = "Gone", Path = missingPath }] },
        });

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

    /// <summary>"teams" and "teammates" are always taken inside Slug, so a name that slugs to either gets a numeric suffix, and a repeat of the same name keeps incrementing (corrections-B3 item 6).</summary>
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
        var paths = new TeammatePaths(dataDir.Options());
        var options = Options.Create(new TeamOptions
        {
            DataDir = dataDir.Path,
            Library = new LibraryOptions { Roots = [new PinnedRootOption { Name = "Old", Path = dataDir.Path }] },
        });
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
        var paths = new TeammatePaths(dataDir.Options());
        var store = new LibraryRootStore(dataDir.Options(), paths, NullLogger<LibraryRootStore>.Instance);

        _ = store.Roots;

        Assert.False(File.Exists(Path.Combine(dataDir.Path, "library-roots.json")));
    }

    /// <summary><see cref="LibraryRootStore.Reset"/> deletes the saved file and restores the configured pinned roots.</summary>
    [Fact]
    public void Reset_DeletesFile_RestoresConfigured()
    {
        using var dataDir = new TempDataDir();
        var paths = new TeammatePaths(dataDir.Options());
        var options = Options.Create(new TeamOptions
        {
            DataDir = dataDir.Path,
            Library = new LibraryOptions { Roots = [new PinnedRootOption { Name = "Configured", Path = dataDir.Path }] },
        });
        var store = new LibraryRootStore(options, paths, NullLogger<LibraryRootStore>.Instance);
        store.Save([new PinnedRootEntry("Saved", dataDir.Path)]);

        store.Reset();

        Assert.False(File.Exists(Path.Combine(dataDir.Path, "library-roots.json")));
        Assert.Contains(store.Roots, r => r.DisplayName == "Configured");
    }

    /// <summary><see cref="LibraryRootStore.Reset"/> also clears any hidden built-in (corrections-B3 item 8).</summary>
    [Fact]
    public void Reset_ClearsHiddenToo()
    {
        using var dataDir = new TempDataDir();
        var paths = new TeammatePaths(dataDir.Options());
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
        var paths = new TeammatePaths(dataDir.Options());
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
        var paths = new TeammatePaths(dataDir.Options());
        var options = Options.Create(new TeamOptions
        {
            DataDir = dataDir.Path,
            Library = new LibraryOptions { Roots = [new PinnedRootOption { Name = "Docs", Path = dataDir.Path }] },
        });
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

    /// <summary>Hiding a pinned (non-built-in) root's id is rejected: only "teams" and "teammates" can be hidden (corrections-B3 item 8).</summary>
    [Fact]
    public void SetHidden_PinnedId_ThrowsArgumentException()
    {
        using var dataDir = new TempDataDir();
        var paths = new TeammatePaths(dataDir.Options());
        var options = Options.Create(new TeamOptions
        {
            DataDir = dataDir.Path,
            Library = new LibraryOptions { Roots = [new PinnedRootOption { Name = "Docs", Path = dataDir.Path }] },
        });
        var store = new LibraryRootStore(options, paths, NullLogger<LibraryRootStore>.Instance);
        var pinnedId = Assert.Single(store.Roots, r => r.Kind == LibraryRootKind.Pinned).Id;

        Assert.Throws<ArgumentException>(() => store.SetHidden(pinnedId, true));
    }

    /// <summary><see cref="LibraryRootStore.Save"/> raises RootsChanged exactly once (corrections-B3 item 9).</summary>
    [Fact]
    public void Save_RaisesRootsChangedOnce()
    {
        using var dataDir = new TempDataDir();
        var paths = new TeammatePaths(dataDir.Options());
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
        var paths = new TeammatePaths(dataDir.Options());
        var options = Options.Create(new TeamOptions
        {
            DataDir = dataDir.Path,
            Library = new LibraryOptions { Roots = [new PinnedRootOption { Name = "Docs", Path = dataDir.Path }] },
        });

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
        var paths = new TeammatePaths(dataDir.Options());
        var store = new LibraryRootStore(dataDir.Options(), paths, NullLogger<LibraryRootStore>.Instance);

        Assert.Throws<InvalidOperationException>(() => store.Save([new PinnedRootEntry("New", dataDir.Path)]));
    }

    /// <summary>
    /// A hand-written fake <see cref="ILogger{T}"/> that records every call, since this repo has no
    /// mocking framework.
    /// </summary>
    /// <typeparam name="T">The category type the recorded logger stands in for.</typeparam>
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        /// <summary>Every call made to this logger so far, in call order.</summary>
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        /// <summary>Not used by this fake: scoping is irrelevant to the tests that need it, so this returns a no-op.</summary>
        /// <typeparam name="TState">The scope state type.</typeparam>
        /// <param name="state">The scope state.</param>
        /// <returns>A no-op <see cref="IDisposable"/>.</returns>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <summary>Always enabled, so every call this fake receives is actually recorded.</summary>
        /// <param name="logLevel">The level being checked.</param>
        /// <returns><see langword="true"/>, always.</returns>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <summary>Records one log call's level and formatted message.</summary>
        /// <typeparam name="TState">The state type carrying this call's structured values.</typeparam>
        /// <param name="logLevel">The call's severity.</param>
        /// <param name="eventId">Unused by this fake.</param>
        /// <param name="state">The call's structured state, passed to <paramref name="formatter"/>.</param>
        /// <param name="exception">The call's exception, if any.</param>
        /// <param name="formatter">Formats <paramref name="state"/> and <paramref name="exception"/> into the message text.</param>
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            this.Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
