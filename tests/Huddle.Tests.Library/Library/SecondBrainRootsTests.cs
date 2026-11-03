using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Tests for the three Second Brain roots of <see cref="LibraryRootStore"/>: <c>raw</c>, <c>archive</c> (read-only)
/// and <c>prompts</c>, which replace the former <c>uploads</c> root.
/// </summary>
public sealed class SecondBrainRootsTests
{
    /// <summary>The Raw root is a writable built-in named "Raw" at <c>{DataDir}/Raw</c>.</summary>
    [Fact]
    public void Roots_Default_IncludeRaw()
    {
        using TempDataDir dataDir = new();

        LibraryRoot raw = Find(dataDir, "raw");

        Assert.Equal("Raw", raw.DisplayName);
        Assert.Equal(LibraryRootKind.Raw, raw.Kind);
        Assert.False(raw.ReadOnly);
        Assert.Equal(Path.GetFullPath(Path.Combine(dataDir.Path, "Raw")), Path.GetFullPath(raw.FullPath));
    }

    /// <summary>The Archive root is a read-only built-in named "Archive" at <c>{DataDir}/Archive</c>.</summary>
    [Fact]
    public void Roots_Default_IncludeArchiveReadOnly()
    {
        using TempDataDir dataDir = new();

        LibraryRoot archive = Find(dataDir, "archive");

        Assert.Equal("Archive", archive.DisplayName);
        Assert.Equal(LibraryRootKind.Archive, archive.Kind);
        Assert.True(archive.ReadOnly);
        Assert.Equal(Path.GetFullPath(Path.Combine(dataDir.Path, "Archive")), Path.GetFullPath(archive.FullPath));
    }

    /// <summary>The Prompts root is a writable built-in named "Prompts" at <c>{DataDir}/Prompts</c>.</summary>
    [Fact]
    public void Roots_Default_IncludePrompts()
    {
        using TempDataDir dataDir = new();

        LibraryRoot prompts = Find(dataDir, "prompts");

        Assert.Equal("Prompts", prompts.DisplayName);
        Assert.Equal(LibraryRootKind.Prompts, prompts.Kind);
        Assert.False(prompts.ReadOnly);
        Assert.Equal(Path.GetFullPath(Path.Combine(dataDir.Path, "Prompts")), Path.GetFullPath(prompts.FullPath));
    }

    /// <summary>No root keeps the retired <c>uploads</c> id.</summary>
    [Fact]
    public void Roots_Default_HaveNoUploadsRoot()
    {
        using TempDataDir dataDir = new();
        LibraryRootStore store = new(dataDir.Options(), new AgentPaths(dataDir.Options()), NullLogger<LibraryRootStore>.Instance);

        Assert.DoesNotContain(store.Roots, r => string.Equals(r.Id, "uploads", StringComparison.Ordinal));
    }

    /// <summary>A pinned root named like a Second Brain built-in cannot take the built-in id, so it gets a different slug.</summary>
    [Theory]
    [InlineData("Raw", "raw")]
    [InlineData("Archive", "archive")]
    [InlineData("Prompts", "prompts")]
    public void Slug_BuiltInName_IsReserved(string name, string reserved)
    {
        string slug = LibraryRootStore.Slug(name, []);

        Assert.NotEqual(reserved, slug);
    }

    private static LibraryRoot Find(TempDataDir dataDir, string id)
    {
        LibraryRootStore store = new(dataDir.Options(), new AgentPaths(dataDir.Options()), NullLogger<LibraryRootStore>.Instance);

        return Assert.Single(store.Roots, r => string.Equals(r.Id, id, StringComparison.Ordinal));
    }
}
