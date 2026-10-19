using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Profile;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.DocHistory;

/// <summary>Pins the Library's History hooks (DocHistory spec §6.4, §6.5): a Human save, create, rename and move each reach the History, and a failed one does not.</summary>
public sealed class LibraryFileServiceHistoryTests
{
    /// <summary>A successful save of a Markdown document adds one Human <c>Edited</c> entry.</summary>
    [Fact]
    public async Task WriteTextAsync_Markdown_RecordsHumanEdit()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "note.md"), "hello");
        (LibraryFileService service, DocHistoryStore store) = Build(fixture);
        LibraryPath path = fixture.Resolve(vault, "note.md");
        LibraryDocumentContent content = await service.ReadAsync(path, ct);
        Assert.NotNull(content.Format);

        LibraryResult<LibraryEntry> result = await service.WriteTextAsync(path, "updated", content.Format, ct);

        Assert.NotNull(result.Value);
        DocHistoryEntry entry = Assert.Single(store.Read(path));
        Assert.Equal(("Emre", DocActorKind.Human, DocChangeKind.Edited, string.Empty), (entry.Actor, entry.ActorKind, entry.Kind, entry.Summary));
    }

    /// <summary>A refused save adds nothing.</summary>
    [Fact]
    public async Task WriteTextAsync_Refused_RecordsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build(teamOptions => teamOptions.Library.MaxEditableBytes = 4);
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "note.md"), "abc");
        (LibraryFileService service, DocHistoryStore store) = Build(fixture);
        LibraryPath path = fixture.Resolve(vault, "note.md");
        LibraryDocumentContent content = await service.ReadAsync(path, ct);
        Assert.NotNull(content.Format);
        File.WriteAllText(Path.Combine(vault, "note.md"), "grew past the cap after it was read");

        LibraryResult<LibraryEntry> result = await service.WriteTextAsync(path, "far longer than the cap", content.Format, ct);

        Assert.Null(result.Value);
        Assert.Empty(store.Read(path));
    }

    /// <summary>Creating a note adds one Human <c>Created</c> entry at the created path.</summary>
    [Fact]
    public async Task CreateFileAsync_RecordsHumanCreate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        (LibraryFileService service, DocHistoryStore store) = Build(fixture);

        LibraryResult<LibraryPath> result = await service.CreateFileAsync(fixture.Resolve(vault, string.Empty), "plan", ct);

        Assert.NotNull(result.Value);
        Assert.Equal(DocChangeKind.Created, Assert.Single(store.Read(result.Value)).Kind);
    }

    /// <summary>Renaming a document carries its History to the new name.</summary>
    [Fact]
    public async Task RenameAsync_CarriesHistory()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "findings.md"), "x");
        (LibraryFileService service, DocHistoryStore store) = Build(fixture);
        LibraryPath path = fixture.Resolve(vault, "findings.md");
        store.Append(path, new DocHistoryEntry(DateTimeOffset.UnixEpoch, "Miriam", DocActorKind.Agent, DocChangeKind.Created, "Draft"));

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(path, "final-findings.md", ct);

        Assert.NotNull(result.Value);
        Assert.Empty(store.Read(path));
        Assert.Equal("Miriam", Assert.Single(store.Read(result.Value.NewPath)).Actor);
    }

    /// <summary>Moving a folder carries the History of the documents inside it.</summary>
    [Fact]
    public async Task MoveAsync_Folder_CarriesHistory()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        Directory.CreateDirectory(Path.Combine(vault, "Research"));
        Directory.CreateDirectory(Path.Combine(vault, "Archive"));
        File.WriteAllText(Path.Combine(vault, "Research", "findings.md"), "x");
        (LibraryFileService service, DocHistoryStore store) = Build(fixture);
        store.Append(fixture.Resolve(vault, "Research/findings.md"), new DocHistoryEntry(DateTimeOffset.UnixEpoch, "Miriam", DocActorKind.Agent, DocChangeKind.Created, "Draft"));

        LibraryResult<LibraryMoveResult> result = await service.MoveAsync(fixture.Resolve(vault, "Research"), fixture.Resolve(vault, "Archive"), ct);

        Assert.NotNull(result.Value);
        Assert.Single(store.Read(fixture.Resolve(vault, "Archive/Research/findings.md")));
    }

    /// <summary>A refused rename leaves the History where it was.</summary>
    [Fact]
    public async Task RenameAsync_Refused_LeavesHistory()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "findings.md"), "x");
        File.WriteAllText(Path.Combine(vault, "taken.md"), "y");
        (LibraryFileService service, DocHistoryStore store) = Build(fixture);
        LibraryPath path = fixture.Resolve(vault, "findings.md");
        store.Append(path, new DocHistoryEntry(DateTimeOffset.UnixEpoch, "Miriam", DocActorKind.Agent, DocChangeKind.Created, "Draft"));

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(path, "taken.md", ct);

        Assert.Null(result.Value);
        Assert.Single(store.Read(path));
    }

    /// <summary>Renaming a folder that differs only in case carries its documents' History instead of losing it.</summary>
    [Fact]
    public async Task RenameAsync_FolderCaseOnly_CarriesHistory()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        Directory.CreateDirectory(Path.Combine(vault, "Research"));
        File.WriteAllText(Path.Combine(vault, "Research", "findings.md"), "x");
        (LibraryFileService service, DocHistoryStore store) = Build(fixture);
        store.Append(fixture.Resolve(vault, "Research/findings.md"), new DocHistoryEntry(DateTimeOffset.UnixEpoch, "Miriam", DocActorKind.Agent, DocChangeKind.Created, "Draft"));

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(fixture.Resolve(vault, "Research"), "research", ct);

        Assert.NotNull(result.Value);
        Assert.Single(store.Read(fixture.Resolve(vault, "research/findings.md")));
    }

    /// <summary>The links rewritten in other notes after a rename are not the Human's saves, so the linking note gets no entry.</summary>
    [Fact]
    public async Task RenameAsync_RewritesLinks_RecordsNothingForTheLinkingNote()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "plan.md"), "Plan.");
        File.WriteAllText(Path.Combine(vault, "a.md"), "See [[plan]].");
        fixture.Reload();
        (LibraryFileService service, DocHistoryStore store) = Build(fixture);

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(fixture.Resolve(vault, "plan.md"), "roadmap.md", ct);

        Assert.NotNull(result.Value);
        Assert.Equal(["a.md"], result.Value.RewrittenNotes);
        Assert.Empty(store.Read(fixture.Resolve(vault, "a.md")));
    }

    /// <summary>Moving a folder whose notes link to each other leaves each note with exactly the entries it had before.</summary>
    [Fact]
    public async Task MoveAsync_Folder_RewrittenNotesKeepOnlyTheirOwnHistory()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        Directory.CreateDirectory(Path.Combine(vault, "Research"));
        Directory.CreateDirectory(Path.Combine(vault, "Archive"));
        File.WriteAllText(Path.Combine(vault, "Research", "findings.md"), "x");
        File.WriteAllText(Path.Combine(vault, "index.md"), "See [[findings]].");
        fixture.Reload();
        (LibraryFileService service, DocHistoryStore store) = Build(fixture);
        DocHistoryEntry draft = new(DateTimeOffset.UnixEpoch, "Miriam", DocActorKind.Agent, DocChangeKind.Created, "Draft");
        store.Append(fixture.Resolve(vault, "Research/findings.md"), draft);

        LibraryResult<LibraryMoveResult> result = await service.MoveAsync(fixture.Resolve(vault, "Research"), fixture.Resolve(vault, "Archive"), ct);

        Assert.NotNull(result.Value);
        Assert.Equal([draft], store.Read(fixture.Resolve(vault, "Archive/Research/findings.md")));
        Assert.Empty(store.Read(fixture.Resolve(vault, "index.md")));
    }
    /// <summary>Builds the service with a recorder whose Human is Emre.</summary>
    /// <param name="fixture">The Library fixture.</param>
    /// <returns>The service and the History store behind it.</returns>
    private static (LibraryFileService Service, DocHistoryStore Store) Build(LibraryFileServiceFixture fixture)
    {
        DocHistoryStore store = new(fixture.BoundOptions, NullLogger<DocHistoryStore>.Instance);
        DocHistoryRecorder recorder = new(store, fixture.BoundOptions, NullLogger<DocHistoryRecorder>.Instance, new ConfiguredHumanIdentity("Emre"), new FakeTimeProvider());
        return (fixture.CreateServiceWithHistory(recorder), store);
    }
}
