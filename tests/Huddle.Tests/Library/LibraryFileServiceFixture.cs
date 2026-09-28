using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// A minimal <see cref="IRecycleBin"/> fake shared by <see cref="LibraryFileServiceFixture"/>: it
/// records every path sent to it, and can be set to fail the next (and every subsequent) call.
/// </summary>
internal sealed class RecordingRecycleBin : IRecycleBin
{
    /// <summary>Every path passed to <see cref="TrySend"/> that succeeded, in call order.</summary>
    public List<string> Sent { get; } = [];

    /// <summary>When set, <see cref="TrySend"/> refuses with this error instead of recording the path.</summary>
    public string? FailureError { get; set; }

    /// <summary>Records <paramref name="fullPath"/> and succeeds, unless <see cref="FailureError"/> is set.</summary>
    /// <param name="fullPath">The path this fake records.</param>
    /// <param name="error"><see cref="FailureError"/> when set; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="false"/> when <see cref="FailureError"/> is set; otherwise <see langword="true"/>.</returns>
    public bool TrySend(string fullPath, [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? error)
    {
        if (this.FailureError is not null)
        {
            error = this.FailureError;
            return false;
        }

        this.Sent.Add(fullPath);
        error = null;
        return true;
    }
}

/// <summary>An isolated fixture: a real <see cref="LibraryRootStore"/> and <see cref="LibraryPathResolver"/>
/// over a temp <c>DataDir</c>, plus the <see cref="RecordingRecycleBin"/> fake. Shared by
/// <see cref="LibraryFileServiceWriteTests"/> and <see cref="LibraryFileServiceCreateTests"/>.</summary>
internal sealed class LibraryFileServiceFixture : IDisposable
{
    private readonly TempDataDir? temp;
    private readonly TeamOptions teamOptions;
    private readonly List<LibraryFileService> createdServices = [];

    private LibraryFileServiceFixture(TempDataDir? temp, TeamOptions teamOptions)
    {
        this.temp = temp;
        this.teamOptions = teamOptions;
        this.DataDir = teamOptions.DataDir;
        TeammatePaths paths = new(Options.Create(teamOptions));
        this.RootStore = new LibraryRootStore(Options.Create(teamOptions), paths, NullLogger<LibraryRootStore>.Instance);
        this.Resolver = new LibraryPathResolver(this.RootStore, Options.Create(teamOptions), NullLogger<LibraryPathResolver>.Instance);
        this.Index = new WikiLinkIndex(this.RootStore, this.Resolver, Options.Create(teamOptions));
    }

    /// <summary>The temp <c>DataDir</c> this fixture's roots live under.</summary>
    public string DataDir { get; }

    /// <summary>The current <see cref="LibraryRootStore"/>, rebuilt by <see cref="Reload"/>.</summary>
    public LibraryRootStore RootStore { get; private set; }

    /// <summary>The current <see cref="LibraryPathResolver"/>, rebuilt by <see cref="Reload"/>.</summary>
    public LibraryPathResolver Resolver { get; private set; }

    /// <summary>The <see cref="WikiLinkIndex"/> most recently wired up by <see cref="CreateService()"/> (D8):
    /// built over this fixture's current <see cref="RootStore"/> and <see cref="Resolver"/>, so tests can
    /// query <c>LinksTo</c>/<c>Backlinks</c>/<c>IsAvailable</c> or call <c>Invalidate</c> after an operation.</summary>
    public WikiLinkIndex Index { get; private set; }

    /// <summary>Builds a fixture with the standard Teams/Teammates layout, optionally customising <see cref="TeamOptions"/> first.</summary>
    /// <param name="configure">An optional callback to customise the bound <see cref="TeamOptions"/> before use.</param>
    public static LibraryFileServiceFixture Build(Action<TeamOptions>? configure = null)
    {
        TempDataDir temp = new();
        Directory.CreateDirectory(Path.Combine(temp.Path, "Teams"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "Teammates"));

        TeamOptions teamOptions = new() { DataDir = temp.Path };
        configure?.Invoke(teamOptions);

        return new LibraryFileServiceFixture(temp, teamOptions);
    }

    /// <summary>
    /// Builds a fixture over an existing <paramref name="dataDir"/> (e.g. a
    /// <see cref="Agency.Huddle.Tests.Acp.Tools.TaskToolHarness"/>'s <c>TempDataDir</c>), so a Library
    /// test and a Tasks stack see the same on-disk Team folders (Task 7.4.t). Ensures <c>Teams/</c> and
    /// <c>Teammates/</c> exist under it but does not own or dispose <paramref name="dataDir"/> itself.
    /// </summary>
    /// <param name="dataDir">The already-existing data directory to attach to.</param>
    /// <param name="configure">An optional callback to customise the bound <see cref="TeamOptions"/> before use.</param>
    public static LibraryFileServiceFixture Attach(string dataDir, Action<TeamOptions>? configure = null)
    {
        Directory.CreateDirectory(Path.Combine(dataDir, "Teams"));
        Directory.CreateDirectory(Path.Combine(dataDir, "Teammates"));

        TeamOptions teamOptions = new() { DataDir = dataDir };
        configure?.Invoke(teamOptions);

        return new LibraryFileServiceFixture(null, teamOptions);
    }

    /// <summary>Creates a pinned root folder named <paramref name="name"/> under the temp root and reloads the store and resolver.</summary>
    /// <param name="name">The pinned root's display name and folder name.</param>
    /// <returns>The pinned root's absolute path.</returns>
    public string CreatePinnedRoot(string name)
    {
        string path = Path.Combine(this.DataDir, name);
        Directory.CreateDirectory(path);

        List<PinnedRootOption> rootsOption = [.. this.teamOptions.Library.Roots ?? [], new PinnedRootOption { Name = name, Path = path }];
        this.teamOptions.Library.Roots = rootsOption;
        this.Reload();
        return path;
    }

    /// <summary>Writes a minimal, valid Teammate definition under the Teammates root and reloads the store
    /// and resolver. Returns the written text.</summary>
    /// <param name="stem">The Teammate folder and definition file stem.</param>
    /// <param name="name">The frontmatter <c>Name</c> value.</param>
    /// <param name="alias">The frontmatter <c>Alias</c> value.</param>
    public string CreateTeammate(string stem, string name, string alias)
    {
        TeammatePaths paths = new(Options.Create(this.teamOptions));
        string text = $"---\nName: {name}\nTitle: Chief of Staff\nAlias: {alias}\n_builtin: chief-of-staff\n---\nbody";
        Directory.CreateDirectory(paths.TeammateFolder(stem));
        File.WriteAllText(paths.DefinitionFile(stem), text);
        this.Reload();
        return text;
    }

    /// <summary>The <see cref="RecordingRecycleBin"/> most recently wired up by <see cref="CreateService()"/>.</summary>
    public RecordingRecycleBin RecycleBin { get; private set; } = new();

    /// <summary>Constructs the service under test, wired to this fixture's resolver and a fresh <see cref="RecordingRecycleBin"/>
    /// (available afterwards as <see cref="RecycleBin"/>).</summary>
    public LibraryFileService CreateService() => this.CreateService(beforeRewriteWriteForTests: null);

    /// <summary>Constructs the service under test, wired to this fixture's resolver, its <see cref="Index"/>, a
    /// fresh <see cref="RecordingRecycleBin"/> (available afterwards as <see cref="RecycleBin"/>), and the D8
    /// rewrite-loop test seam <paramref name="beforeRewriteWriteForTests"/>: called after a linking note is
    /// read and rewritten, before it is written back, so a test can mutate the note on disk to prove the
    /// "changed during the rename" path without a race.</summary>
    /// <param name="beforeRewriteWriteForTests">Runs after a note's rewritten text is computed and before it
    /// is written; <see langword="null"/> for the ordinary path.</param>
    public LibraryFileService CreateService(Func<LibraryPath, Task>? beforeRewriteWriteForTests)
    {
        this.RecycleBin = new RecordingRecycleBin();
        LibraryFileService service = new(
            this.Resolver,
            this.RecycleBin,
            index: this.Index,
            options: Options.Create(this.teamOptions),
            logger: NullLogger<LibraryFileService>.Instance,
            beforeRewriteWriteForTests: beforeRewriteWriteForTests);
        this.createdServices.Add(service);
        return service;
    }

    /// <summary>Resolves <paramref name="relativePath"/> inside whichever configured root's full path equals
    /// <paramref name="rootPath"/> or contains it (so a caller can pass a folder several levels under a root,
    /// e.g. a Teammate's work folder, and not just a pinned root's own path).</summary>
    /// <param name="rootPath">A configured root's absolute path, or a folder underneath one.</param>
    /// <param name="relativePath">The path to resolve, relative to <paramref name="rootPath"/>.</param>
    public LibraryPath Resolve(string rootPath, string relativePath)
    {
        foreach (LibraryRoot candidate in this.RootStore.Roots)
        {
            if (FolderSnapshot.PathComparer.Equals(candidate.FullPath, rootPath))
            {
                Assert.True(this.Resolver.TryResolve(candidate.Id, relativePath, out LibraryPath? resolved, out _));
                Assert.NotNull(resolved);
                return resolved;
            }

            string prefix = candidate.FullPath + Path.DirectorySeparatorChar;
            if (rootPath.Length > prefix.Length && FolderSnapshot.PathComparer.Equals(rootPath[..prefix.Length], prefix))
            {
                string underRoot = rootPath[prefix.Length..].Replace(Path.DirectorySeparatorChar, '/');
                string combined = relativePath.Length == 0 ? underRoot : $"{underRoot}/{relativePath}";
                Assert.True(this.Resolver.TryResolve(candidate.Id, combined, out LibraryPath? resolved, out _));
                Assert.NotNull(resolved);
                return resolved;
            }
        }

        throw new InvalidOperationException($"No configured root at or above '{rootPath}'.");
    }

    /// <summary>Resolves <paramref name="relativePath"/> inside the Teams root.</summary>
    /// <param name="relativePath">The path to resolve, relative to the Teams root.</param>
    public LibraryPath ResolveTeams(string relativePath)
    {
        Assert.True(this.Resolver.TryResolve("teams", relativePath, out LibraryPath? resolved, out _));
        Assert.NotNull(resolved);
        return resolved;
    }

    /// <summary>Resolves <paramref name="relativePath"/> inside the Teammates root (a folder, not a definition file).</summary>
    /// <param name="relativePath">The path to resolve, relative to the Teammates root.</param>
    public LibraryPath ResolveTeammatesFolder(string relativePath)
    {
        Assert.True(this.Resolver.TryResolve("teammates", relativePath, out LibraryPath? resolved, out _));
        Assert.NotNull(resolved);
        return resolved;
    }

    /// <summary>Resolves the definition file for the Teammate with stem <paramref name="stem"/>.</summary>
    /// <param name="stem">The Teammate's folder and definition file stem.</param>
    public LibraryPath ResolveTeammates(string stem)
    {
        Assert.True(this.Resolver.TryResolve("teammates", $"{stem}/{stem}.md", out LibraryPath? resolved, out _));
        Assert.NotNull(resolved);
        return resolved;
    }

    /// <summary>Reloads the root store and resolver, picking up on-disk changes made since construction.</summary>
    public void Reload()
    {
        TeammatePaths paths = new(Options.Create(this.teamOptions));
        this.Index.Dispose();
        this.RootStore = new LibraryRootStore(Options.Create(this.teamOptions), paths, NullLogger<LibraryRootStore>.Instance);
        this.Resolver = new LibraryPathResolver(this.RootStore, Options.Create(this.teamOptions), NullLogger<LibraryPathResolver>.Instance);
        this.Index = new WikiLinkIndex(this.RootStore, this.Resolver, Options.Create(this.teamOptions));
    }

    /// <summary>Disposes the temp <c>DataDir</c>, when this fixture owns one (see <see cref="Attach"/>).</summary>
    public void Dispose()
    {
        foreach (LibraryFileService service in this.createdServices)
        {
            service.Dispose();
        }

        this.Index.Dispose();
        this.temp?.Dispose();
    }
}
