using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.SkillSources;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Library;

/// <summary>Pins the Skill Sources root of <see cref="LibraryRootStore"/> (Skill Packs spec §6.11.2): its place, kind and
/// read-only flag, that it follows <see cref="SkillSourcesSettings"/>, its reserved slug, and Hide.</summary>
public sealed class LibraryRootStoreSkillSourcesTests : IDisposable
{
    private readonly TempDataDir dir = new();

    /// <summary>Deletes the temp data folder.</summary>
    public void Dispose()
    {
        this.dir.Dispose();
    }

    /// <summary>The last built-in root is <c>skill-sources</c>, of kind SkillSources, read only, at the settings' folder; Archive and Memory backups are the only other read-only roots.</summary>
    [Fact]
    public void Roots_EndWithTheReadOnlySkillSourcesRoot_AtTheSettingsFolder()
    {
        SkillSourcesSettings settings = this.Settings();
        LibraryRootStore store = this.Store(settings);

        LibraryRoot last = store.Roots[^1];

        Assert.Equal(LibraryRootStore.SkillSourcesRootId, last.Id);
        Assert.Equal("Skill Sources", last.DisplayName);
        Assert.Equal(LibraryRootKind.SkillSources, last.Kind);
        Assert.True(last.ReadOnly);
        Assert.Equal(settings.Dir, last.FullPath);
        Assert.True(store.Roots.Single(root => root.Kind == LibraryRootKind.Archive).ReadOnly);
        Assert.All(store.Roots.Take(store.Roots.Count - 1).Where(root => root.Kind is not (LibraryRootKind.Archive or LibraryRootKind.MemoryBackups)), root => Assert.False(root.ReadOnly));
    }

    /// <summary>Changing the folder setting raises <c>RootsChanged</c> once and the root shows the new path, keeping the
    /// pinned roots and the hidden set.</summary>
    [Fact]
    public void SetDir_RaisesRootsChangedOnce_AndKeepsPinnedAndHidden()
    {
        SkillSourcesSettings settings = this.Settings();
        LibraryRootStore store = this.Store(settings);
        string pinned = Path.Combine(this.dir.Path, "Docs");
        string moved = Path.Combine(this.dir.Path, "Elsewhere");
        Directory.CreateDirectory(pinned);
        Directory.CreateDirectory(moved);
        Assert.Null(store.TryPin("Docs", pinned));
        store.SetHidden("teams", true);
        int raised = 0;
        store.RootsChanged += () => raised++;

        Assert.True(settings.SetDir(moved).Changed);

        Assert.Equal(1, raised);
        Assert.Equal(moved, store.Roots.Single(root => root.Id == LibraryRootStore.SkillSourcesRootId).FullPath);
        Assert.Contains(store.Roots, root => root.FullPath == pinned);
        Assert.DoesNotContain(store.VisibleRoots, root => root.Id == "teams");
    }

    /// <summary>A pinned root named "Skill Sources" does not take the built-in's id.</summary>
    [Fact]
    public void TryPin_NamedSkillSources_GetsAnotherSlug()
    {
        LibraryRootStore store = this.Store(this.Settings());
        string folder = Path.Combine(this.dir.Path, "Mine");
        Directory.CreateDirectory(folder);

        Assert.Null(store.TryPin("Skill Sources", folder));

        LibraryRoot pinned = store.Roots.Single(root => root.FullPath == folder);
        Assert.NotEqual(LibraryRootStore.SkillSourcesRootId, pinned.Id);
        Assert.False(pinned.ReadOnly);
    }

    /// <summary>Hiding the root takes it out of <c>VisibleRoots</c> only; it stays in <c>Roots</c>.</summary>
    [Fact]
    public void SetHidden_SkillSources_HidesFromVisibleRootsOnly()
    {
        LibraryRootStore store = this.Store(this.Settings());

        store.SetHidden(LibraryRootStore.SkillSourcesRootId, true);

        Assert.DoesNotContain(store.VisibleRoots, root => root.Id == LibraryRootStore.SkillSourcesRootId);
        Assert.Contains(store.Roots, root => root.Id == LibraryRootStore.SkillSourcesRootId);
    }

    /// <summary>The settings over this data folder.</summary>
    private SkillSourcesSettings Settings()
    {
        return new SkillSourcesSettings(this.dir.Options(), NullLogger<SkillSourcesSettings>.Instance);
    }

    /// <summary>A store that follows <paramref name="settings"/>.</summary>
    private LibraryRootStore Store(SkillSourcesSettings settings)
    {
        IOptions<TeamOptions> options = this.dir.Options();
        return new LibraryRootStore(options, new AgentPaths(options), NullLogger<LibraryRootStore>.Instance, settings);
    }
}
