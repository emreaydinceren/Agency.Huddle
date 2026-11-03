using Agency.Huddle.App.Library;
using Agency.Huddle.Tests.References;

namespace Agency.Huddle.Tests.Library.Archive;

/// <summary>A Task attachment path written before its Team was archived still opens, from the Archive (read-only), through <c>TryResolveForRead</c>.</summary>
public sealed class ArchivedAttachmentResolveTests
{
    /// <summary>A Teams path whose file now lives only under Archive resolves to the Archive twin and exists.</summary>
    [Fact]
    public async Task ResolvePath_FileMovedWithArchivedTeam_ResolvesToArchive()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        string archived = Path.Combine(fixture.DataDir, "Archive", "Home", "raw");
        Directory.CreateDirectory(archived);
        await File.WriteAllTextAsync(Path.Combine(archived, "notes.txt"), "hi", ct);

        LibraryReference? reference = fixture.LibraryResolver.ResolvePath(Path.Combine(fixture.DataDir, "Teams", "Home", "raw", "notes.txt"));

        Assert.NotNull(reference);
        Assert.True(reference.Exists);
        Assert.Equal("Home/raw/notes.txt", reference.RelativePath);
        Assert.Equal("archive", reference.RootId);
    }

    /// <summary>A Teams path with no Archive twin still reports a missing file.</summary>
    [Fact]
    public async Task ResolvePath_NoArchiveTwin_StillMissing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);

        LibraryReference? reference = fixture.LibraryResolver.ResolvePath(Path.Combine(fixture.DataDir, "Teams", "Home", "raw", "notes.txt"));

        Assert.NotNull(reference);
        Assert.False(reference.Exists);
    }
}
