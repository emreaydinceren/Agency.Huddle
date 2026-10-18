using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Tests for <see cref="LibraryReferenceResolver"/> (Spec §6.6, corrections-B5 items 22-23, D9 item 39):
/// <see cref="ILibraryReferenceResolver.ResolvePath"/>, <see cref="ILibraryReferenceResolver.ResolveWikiLink"/>
/// and <see cref="ILibraryNoteResolver.ResolveRelative"/> over a real <see cref="LibraryPathResolver"/> and
/// <see cref="WikiLinkIndex"/>, on a temp tree.
/// </summary>
public sealed class LibraryReferenceResolverTests
{
    private static LibraryReferenceResolver CreateResolver(LibraryFileServiceFixture fixture, bool enabled = true) =>
        new(fixture.Resolver, fixture.Index, Options.Create(new TeamOptions { Library = new LibraryOptions { Enabled = enabled } }));

    /// <summary>A path that resolves inside a root and exists on disk resolves with <c>Exists</c> true.</summary>
    [Fact]
    public void ResolvePath_Existing_ExistsTrue()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        LibraryPath team = fixture.ResolveTeams("Marketing");
        Directory.CreateDirectory(team.FullPath);
        string filePath = Path.Combine(team.FullPath, "plan.md");
        File.WriteAllText(filePath, "body");
        LibraryReferenceResolver resolver = CreateResolver(fixture);

        LibraryReference? reference = resolver.ResolvePath(filePath);

        Assert.NotNull(reference);
        Assert.Equal("teams", reference.RootId);
        Assert.Equal("Marketing/plan.md", reference.RelativePath);
        Assert.True(reference.Exists);
        Assert.False(reference.IsAmbiguous);
    }

    /// <summary>A path that resolves inside a root but has no file on disk resolves with <c>Exists</c> false, not null.</summary>
    [Fact]
    public void ResolvePath_MissingFileInRoot_ExistsFalse()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        LibraryPath team = fixture.ResolveTeams("Marketing");
        Directory.CreateDirectory(team.FullPath);
        string filePath = Path.Combine(team.FullPath, "missing.md");
        LibraryReferenceResolver resolver = CreateResolver(fixture);

        LibraryReference? reference = resolver.ResolvePath(filePath);

        Assert.NotNull(reference);
        Assert.Equal("Marketing/missing.md", reference.RelativePath);
        Assert.False(reference.Exists);
    }

    /// <summary>A path outside every configured root resolves to null.</summary>
    [Fact]
    public void ResolvePath_Outside_Null()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        LibraryReferenceResolver resolver = CreateResolver(fixture);
        string outsidePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "x.md");

        LibraryReference? reference = resolver.ResolvePath(outsidePath);

        Assert.Null(reference);
    }

    /// <summary>A path inside a Team folder's reserved <c>_tasks</c> subtree resolves to null.</summary>
    [Fact]
    public void ResolvePath_TasksFile_Null()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        LibraryPath team = fixture.ResolveTeams("Marketing");
        Directory.CreateDirectory(Path.Combine(team.FullPath, "_tasks"));
        string filePath = Path.Combine(team.FullPath, "_tasks", "MKT-0001.md");
        File.WriteAllText(filePath, "body");
        LibraryReferenceResolver resolver = CreateResolver(fixture);

        LibraryReference? reference = resolver.ResolvePath(filePath);

        Assert.Null(reference);
    }

    /// <summary>The allowed neighbour of the reserved <c>_tasks</c> row: a sibling note beside it resolves fine.</summary>
    [Fact]
    public void ResolvePath_TasksFileSibling_NotNull()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        LibraryPath team = fixture.ResolveTeams("Marketing");
        Directory.CreateDirectory(Path.Combine(team.FullPath, "_tasks"));
        string siblingPath = Path.Combine(team.FullPath, "note.md");
        File.WriteAllText(siblingPath, "body");
        LibraryReferenceResolver resolver = CreateResolver(fixture);

        LibraryReference? reference = resolver.ResolvePath(siblingPath);

        Assert.NotNull(reference);
        Assert.Equal("Marketing/note.md", reference.RelativePath);
        Assert.True(reference.Exists);
    }

    /// <summary>A folder path inside a root resolves with <c>Exists</c> true.</summary>
    [Fact]
    public void ResolvePath_Folder_ExistsTrue()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        LibraryPath team = fixture.ResolveTeams("Marketing");
        Directory.CreateDirectory(team.FullPath);
        LibraryReferenceResolver resolver = CreateResolver(fixture);

        LibraryReference? reference = resolver.ResolvePath(team.FullPath);

        Assert.NotNull(reference);
        Assert.Equal("Marketing", reference.RelativePath);
        Assert.True(reference.Exists);
    }

    /// <summary>A differently-cased spelling of an existing path still resolves on a case-insensitive file system.</summary>
    [Fact]
    public void ResolvePath_DifferentCase_ExistsTrue()
    {
        if (OperatingSystem.IsLinux())
        {
            Assert.Skip("Case-sensitive file system: a differently-cased path is a different file.");
            return;
        }

        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        LibraryPath team = fixture.ResolveTeams("Marketing");
        Directory.CreateDirectory(team.FullPath);
        string filePath = Path.Combine(team.FullPath, "plan.md");
        File.WriteAllText(filePath, "body");
        LibraryReferenceResolver resolver = CreateResolver(fixture);

        LibraryReference? reference = resolver.ResolvePath(filePath.ToUpperInvariant());

        Assert.NotNull(reference);
        Assert.True(reference.Exists);
    }

    /// <summary>A wikilink that the root's index resolves comes back as an existing, non-ambiguous reference.</summary>
    [Fact]
    public void ResolveWikiLink_UsesIndex()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        LibraryPath team = fixture.ResolveTeams("Marketing");
        Directory.CreateDirectory(team.FullPath);
        File.WriteAllText(Path.Combine(team.FullPath, "source.md"), "[[target]]");
        File.WriteAllText(Path.Combine(team.FullPath, "target.md"), "body");
        LibraryReferenceResolver resolver = CreateResolver(fixture);
        LibraryPath from = fixture.ResolveTeams("Marketing/source.md");
        WikiLink link = new("target", null, null, false, 1, 0, 11);

        LibraryReference? reference = resolver.ResolveWikiLink(from, link);

        Assert.NotNull(reference);
        Assert.Equal("teams", reference.RootId);
        Assert.Equal("Marketing/target.md", reference.RelativePath);
        Assert.True(reference.Exists);
        Assert.False(reference.IsAmbiguous);
    }

    /// <summary>Two notes named alike, equidistant from the linking note, resolve ambiguously to the ordinal-first path.</summary>
    [Fact]
    public void ResolveWikiLink_Ambiguous_OrdinalFirstPath()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        LibraryPath team = fixture.ResolveTeams("Marketing");
        Directory.CreateDirectory(Path.Combine(team.FullPath, "Alpha"));
        Directory.CreateDirectory(Path.Combine(team.FullPath, "Beta"));
        File.WriteAllText(Path.Combine(team.FullPath, "Alpha", "target.md"), "body");
        File.WriteAllText(Path.Combine(team.FullPath, "Beta", "target.md"), "body");
        File.WriteAllText(Path.Combine(team.FullPath, "source.md"), "[[target]]");
        LibraryReferenceResolver resolver = CreateResolver(fixture);
        LibraryPath from = fixture.ResolveTeams("Marketing/source.md");
        WikiLink link = new("target", null, null, false, 1, 0, 11);

        LibraryReference? reference = resolver.ResolveWikiLink(from, link);

        Assert.NotNull(reference);
        Assert.True(reference.IsAmbiguous);
        Assert.Equal("Marketing/Alpha/target.md", reference.RelativePath);
    }

    /// <summary>An unresolved wikilink (or an unavailable index) never throws, and comes back with the link's
    /// own target as the relative path, unresolved.</summary>
    [Fact]
    public void ResolveWikiLink_Unresolved_ExistsFalse_TargetAsRelative()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build(o => o.Library.MaxIndexedFiles = 1);
        LibraryPath team = fixture.ResolveTeams("Marketing");
        Directory.CreateDirectory(team.FullPath);
        File.WriteAllText(Path.Combine(team.FullPath, "one.md"), "body");
        File.WriteAllText(Path.Combine(team.FullPath, "two.md"), "body");
        File.WriteAllText(Path.Combine(team.FullPath, "source.md"), "[[missing]]");
        LibraryReferenceResolver resolver = CreateResolver(fixture);
        LibraryPath from = fixture.ResolveTeams("Marketing/source.md");
        WikiLink link = new("missing", null, null, false, 1, 0, 11);

        LibraryReference? reference = resolver.ResolveWikiLink(from, link);

        Assert.NotNull(reference);
        Assert.False(reference.Exists);
        Assert.Equal("missing", reference.RelativePath);
    }

    /// <summary>A relative link one folder up from the linking note.</summary>
    [Fact]
    public void ResolveRelative_UpOneFolder()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        Directory.CreateDirectory(Path.Combine(fixture.DataDir, "Teams", "Marketing", "Launch Q4"));
        File.WriteAllText(Path.Combine(fixture.DataDir, "Teams", "Marketing", "plan.md"), "body");
        LibraryReferenceResolver resolver = CreateResolver(fixture);
        LibraryPath from = fixture.ResolveTeams("Marketing/Launch Q4/a.md");

        LibraryReference? reference = resolver.ResolveRelative(from, "../plan.md");

        Assert.NotNull(reference);
        Assert.Equal("Marketing/plan.md", reference.RelativePath);
        Assert.True(reference.Exists);
    }

    /// <summary>A relative link to the current folder (<c>./a.md</c>).</summary>
    [Fact]
    public void ResolveRelative_SameFolder()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        Directory.CreateDirectory(Path.Combine(fixture.DataDir, "Teams", "Marketing", "Launch Q4"));
        File.WriteAllText(Path.Combine(fixture.DataDir, "Teams", "Marketing", "Launch Q4", "a.md"), "body");
        LibraryReferenceResolver resolver = CreateResolver(fixture);
        LibraryPath from = fixture.ResolveTeams("Marketing/Launch Q4/source.md");

        LibraryReference? reference = resolver.ResolveRelative(from, "./a.md");

        Assert.NotNull(reference);
        Assert.Equal("Marketing/Launch Q4/a.md", reference.RelativePath);
        Assert.True(reference.Exists);
    }

    /// <summary>A relative link into a sub-folder.</summary>
    [Fact]
    public void ResolveRelative_SubFolder()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        Directory.CreateDirectory(Path.Combine(fixture.DataDir, "Teams", "Marketing", "Launch Q4", "sub"));
        File.WriteAllText(Path.Combine(fixture.DataDir, "Teams", "Marketing", "Launch Q4", "sub", "x.md"), "body");
        LibraryReferenceResolver resolver = CreateResolver(fixture);
        LibraryPath from = fixture.ResolveTeams("Marketing/Launch Q4/a.md");

        LibraryReference? reference = resolver.ResolveRelative(from, "sub/x.md");

        Assert.NotNull(reference);
        Assert.Equal("Marketing/Launch Q4/sub/x.md", reference.RelativePath);
        Assert.True(reference.Exists);
    }

    /// <summary>A URL-encoded relative link is decoded before resolution.</summary>
    [Fact]
    public void ResolveRelative_UrlEncoded_Decoded()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        Directory.CreateDirectory(Path.Combine(fixture.DataDir, "Teams", "Marketing", "Launch Q4"));
        File.WriteAllText(Path.Combine(fixture.DataDir, "Teams", "Marketing", "Launch Q4", "plan.md"), "body");
        LibraryReferenceResolver resolver = CreateResolver(fixture);
        LibraryPath from = fixture.ResolveTeams("Marketing/a.md");

        LibraryReference? reference = resolver.ResolveRelative(from, "Launch%20Q4/plan.md");

        Assert.NotNull(reference);
        Assert.Equal("Marketing/Launch Q4/plan.md", reference.RelativePath);
        Assert.True(reference.Exists);
    }

    /// <summary>A root-anchored link (leading <c>/</c>) resolves from the Library root, not the note's folder, on every OS.</summary>
    [Fact]
    public void ResolveRelative_RootAnchored_ResolvesFromRoot()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        Directory.CreateDirectory(Path.Combine(fixture.DataDir, "Teams", "Marketing", "Launch Q4", "Deep"));
        File.WriteAllText(Path.Combine(fixture.DataDir, "Teams", "Marketing", "plan.md"), "body");
        LibraryReferenceResolver resolver = CreateResolver(fixture);
        LibraryPath from = fixture.ResolveTeams("Marketing/Launch Q4/Deep/a.md");

        LibraryReference? reference = resolver.ResolveRelative(from, "/Marketing/plan.md");

        Assert.NotNull(reference);
        Assert.Equal("Marketing/plan.md", reference.RelativePath);
        Assert.True(reference.Exists);
    }

    /// <summary>A Windows drive-letter path is not a relative file link (it matches the scheme check, by design).</summary>
    [Fact]
    public void ResolveRelative_DriveLetter_Null()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        LibraryReferenceResolver resolver = CreateResolver(fixture);
        LibraryPath from = fixture.ResolveTeams("Marketing/a.md");

        LibraryReference? reference = resolver.ResolveRelative(from, "C:\\x\\plan.md");

        Assert.Null(reference);
    }

    /// <summary>A <c>file://</c> URL is not a relative file link.</summary>
    [Fact]
    public void ResolveRelative_FileUrl_Null()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        LibraryReferenceResolver resolver = CreateResolver(fixture);
        LibraryPath from = fixture.ResolveTeams("Marketing/a.md");

        LibraryReference? reference = resolver.ResolveRelative(from, "file:///x");

        Assert.Null(reference);
    }

    /// <summary>A trailing <c>#fragment</c> is stripped before resolution.</summary>
    [Fact]
    public void ResolveRelative_TrailingFragment_Stripped()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        Directory.CreateDirectory(Path.Combine(fixture.DataDir, "Teams", "Marketing", "Launch Q4"));
        File.WriteAllText(Path.Combine(fixture.DataDir, "Teams", "Marketing", "plan.md"), "body");
        LibraryReferenceResolver resolver = CreateResolver(fixture);
        LibraryPath from = fixture.ResolveTeams("Marketing/Launch Q4/a.md");

        LibraryReference? reference = resolver.ResolveRelative(from, "../plan.md#Dates");

        Assert.NotNull(reference);
        Assert.Equal("Marketing/plan.md", reference.RelativePath);
        Assert.True(reference.Exists);
    }

    /// <summary>A trailing <c>?query</c> is stripped before resolution.</summary>
    [Fact]
    public void ResolveRelative_TrailingQuery_Stripped()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        Directory.CreateDirectory(Path.Combine(fixture.DataDir, "Teams", "Marketing"));
        File.WriteAllText(Path.Combine(fixture.DataDir, "Teams", "Marketing", "plan.md"), "body");
        LibraryReferenceResolver resolver = CreateResolver(fixture);
        LibraryPath from = fixture.ResolveTeams("Marketing/a.md");

        LibraryReference? reference = resolver.ResolveRelative(from, "plan.md?v=1");

        Assert.NotNull(reference);
        Assert.Equal("Marketing/plan.md", reference.RelativePath);
        Assert.True(reference.Exists);
    }

    /// <summary>An absolute HTTP URL is not a relative file link.</summary>
    [Fact]
    public void ResolveRelative_HttpUrl_Null()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        LibraryReferenceResolver resolver = CreateResolver(fixture);
        LibraryPath from = fixture.ResolveTeams("Marketing/a.md");

        LibraryReference? reference = resolver.ResolveRelative(from, "http://example.com/x");

        Assert.Null(reference);
    }

    /// <summary>A <c>mailto:</c> link is not a relative file link.</summary>
    [Fact]
    public void ResolveRelative_Mailto_Null()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        LibraryReferenceResolver resolver = CreateResolver(fixture);
        LibraryPath from = fixture.ResolveTeams("Marketing/a.md");

        LibraryReference? reference = resolver.ResolveRelative(from, "mailto:someone@example.com");

        Assert.Null(reference);
    }

    /// <summary>A bare heading anchor is not a relative file link.</summary>
    [Fact]
    public void ResolveRelative_HeadingAnchor_Null()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        LibraryReferenceResolver resolver = CreateResolver(fixture);
        LibraryPath from = fixture.ResolveTeams("Marketing/a.md");

        LibraryReference? reference = resolver.ResolveRelative(from, "#heading");

        Assert.Null(reference);
    }

    /// <summary>A relative link that walks above the Library root it started in resolves to null.</summary>
    [Fact]
    public void ResolveRelative_EscapingRoot_Null()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        LibraryReferenceResolver resolver = CreateResolver(fixture);
        LibraryPath from = fixture.ResolveTeams("Marketing/a.md");

        LibraryReference? reference = resolver.ResolveRelative(from, "../../../../etc/passwd");

        Assert.Null(reference);
    }

    /// <summary>With the Library disabled, <c>ResolvePath</c> resolves nothing.</summary>
    [Fact]
    public void LibraryDisabled_ResolvePath_Null()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        LibraryPath team = fixture.ResolveTeams("Marketing");
        Directory.CreateDirectory(team.FullPath);
        string filePath = Path.Combine(team.FullPath, "plan.md");
        File.WriteAllText(filePath, "body");
        LibraryReferenceResolver resolver = CreateResolver(fixture, enabled: false);

        LibraryReference? reference = resolver.ResolvePath(filePath);

        Assert.Null(reference);
    }

    /// <summary>With the Library disabled, <c>ResolveWikiLink</c> resolves nothing.</summary>
    [Fact]
    public void LibraryDisabled_ResolveWikiLink_Null()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        LibraryPath team = fixture.ResolveTeams("Marketing");
        Directory.CreateDirectory(team.FullPath);
        File.WriteAllText(Path.Combine(team.FullPath, "target.md"), "body");
        File.WriteAllText(Path.Combine(team.FullPath, "source.md"), "[[target]]");
        LibraryReferenceResolver resolver = CreateResolver(fixture, enabled: false);
        LibraryPath from = fixture.ResolveTeams("Marketing/source.md");
        WikiLink link = new("target", null, null, false, 1, 0, 11);

        LibraryReference? reference = resolver.ResolveWikiLink(from, link);

        Assert.Null(reference);
    }

    /// <summary>With the Library disabled, <c>ResolveRelative</c> resolves nothing.</summary>
    [Fact]
    public void LibraryDisabled_ResolveRelative_Null()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        Directory.CreateDirectory(Path.Combine(fixture.DataDir, "Teams", "Marketing", "Launch Q4"));
        File.WriteAllText(Path.Combine(fixture.DataDir, "Teams", "Marketing", "plan.md"), "body");
        LibraryReferenceResolver resolver = CreateResolver(fixture, enabled: false);
        LibraryPath from = fixture.ResolveTeams("Marketing/Launch Q4/a.md");

        LibraryReference? reference = resolver.ResolveRelative(from, "../plan.md");

        Assert.Null(reference);
    }
}
