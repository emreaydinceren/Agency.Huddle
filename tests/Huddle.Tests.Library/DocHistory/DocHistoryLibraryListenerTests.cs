using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Profile;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.DocHistory;

/// <summary>Pins <see cref="DocHistoryLibraryListener"/>: it turns the Library's events into History entries, and stops when disposed (S-A6).</summary>
public sealed class DocHistoryLibraryListenerTests
{
    /// <summary>A HumanSaved event records one Human <c>Edited</c> entry.</summary>
    [Fact]
    public void HumanSaved_RecordsHumanEdit()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "note.md"), "hello");
        LibraryEvents events = new();
        (DocHistoryLibraryListener listener, DocHistoryStore store) = Build(fixture, events);
        using DocHistoryLibraryListener owned = listener;
        LibraryPath path = fixture.Resolve(vault, "note.md");

        events.RaiseHumanSaved(path);

        DocHistoryEntry entry = Assert.Single(store.Read(path));
        Assert.Equal(("Emre", DocActorKind.Human, DocChangeKind.Edited), (entry.Actor, entry.ActorKind, entry.Kind));
    }

    /// <summary>A HumanCreated event records one Human <c>Created</c> entry.</summary>
    [Fact]
    public void HumanCreated_RecordsCreated()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "plan.md"), "x");
        LibraryEvents events = new();
        (DocHistoryLibraryListener listener, DocHistoryStore store) = Build(fixture, events);
        using DocHistoryLibraryListener owned = listener;
        LibraryPath path = fixture.Resolve(vault, "plan.md");

        events.RaiseHumanCreated(path);

        Assert.Equal(DocChangeKind.Created, Assert.Single(store.Read(path)).Kind);
    }

    /// <summary>An uploaded Markdown file, through the real service, listener and store, has one Human <c>Created</c> entry (SU1).</summary>
    [Fact]
    public async Task Upload_Markdown_RecordsOneHumanCreated()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryEvents events = new();
        (DocHistoryLibraryListener listener, DocHistoryStore store) = Build(fixture, events);
        using DocHistoryLibraryListener owned = listener;
        LibraryFileService service = fixture.CreateServiceWithEvents(events);

        LibraryResult<LibraryUpload> saved = await service.CreateFromStreamAsync(fixture.Resolve(vault, string.Empty), "notes.md", new MemoryStream("# Notes"u8.ToArray()), 1024 * 1024, ct);

        Assert.NotNull(saved.Value);
        DocHistoryEntry entry = Assert.Single(store.Read(saved.Value.File));
        Assert.Equal(("Emre", DocActorKind.Human, DocChangeKind.Created), (entry.Actor, entry.ActorKind, entry.Kind));
    }

    /// <summary>An uploaded PNG, through the same chain, has no History (only Markdown has one).</summary>
    [Fact]
    public async Task Upload_Png_RecordsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryEvents events = new();
        (DocHistoryLibraryListener listener, DocHistoryStore store) = Build(fixture, events);
        using DocHistoryLibraryListener owned = listener;
        LibraryFileService service = fixture.CreateServiceWithEvents(events);
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02, 0x03];

        LibraryResult<LibraryUpload> saved = await service.CreateFromStreamAsync(fixture.Resolve(vault, string.Empty), "photo.png", new MemoryStream(png), 1024 * 1024, ct);

        Assert.NotNull(saved.Value);
        Assert.Empty(store.Read(saved.Value.File));
    }

    /// <summary>Deleting an unsent Markdown upload takes its History file with it, so a later upload of the same name starts clean (SU2).</summary>
    [Fact]
    public async Task DeleteUnsentUpload_Markdown_RemovesItsHistory()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryEvents events = new();
        (DocHistoryLibraryListener listener, DocHistoryStore store) = Build(fixture, events);
        using DocHistoryLibraryListener owned = listener;
        LibraryFileService service = fixture.CreateServiceWithEvents(events);
        LibraryResult<LibraryUpload> saved = await service.CreateFromStreamAsync(fixture.Resolve(vault, string.Empty), "notes.md", new MemoryStream("# Notes"u8.ToArray()), 1024 * 1024, ct);
        Assert.NotNull(saved.Value);
        store.Append(saved.Value.File, new DocHistoryEntry(DateTimeOffset.UnixEpoch, "Miriam", DocActorKind.Agent, DocChangeKind.Edited, "Tidied"));
        Assert.True(File.Exists(store.FileFor(saved.Value.File)));

        await service.DeleteUnsentUploadAsync(saved.Value.File, ct);

        Assert.False(File.Exists(store.FileFor(saved.Value.File)));
        Assert.Empty(store.Read(saved.Value.File));
    }

    /// <summary>A Moved event carries the History from the old path to the new one.</summary>
    [Fact]
    public void Moved_MovesTheHistory()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "findings.md"), "x");
        LibraryEvents events = new();
        (DocHistoryLibraryListener listener, DocHistoryStore store) = Build(fixture, events);
        using DocHistoryLibraryListener owned = listener;
        LibraryPath from = fixture.Resolve(vault, "findings.md");
        store.Append(from, new DocHistoryEntry(DateTimeOffset.UnixEpoch, "Miriam", DocActorKind.Agent, DocChangeKind.Created, "Draft"));
        File.Move(Path.Combine(vault, "findings.md"), Path.Combine(vault, "final.md"));
        LibraryPath to = fixture.Resolve(vault, "final.md");

        events.RaiseMoved(new LibraryItemMoved(from, to));

        Assert.Empty(store.Read(from));
        Assert.Equal("Miriam", Assert.Single(store.Read(to)).Actor);
    }

    /// <summary>After Dispose a raise records nothing.</summary>
    [Fact]
    public void Dispose_StopsRecording()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "note.md"), "hello");
        LibraryEvents events = new();
        (DocHistoryLibraryListener listener, DocHistoryStore store) = Build(fixture, events);
        LibraryPath path = fixture.Resolve(vault, "note.md");
        listener.Dispose();

        events.RaiseHumanSaved(path);
        events.RaiseHumanCreated(path);

        Assert.Empty(store.Read(path));
    }

    /// <summary>Builds a listener over a recorder whose Human is Emre.</summary>
    /// <param name="fixture">The Library fixture.</param>
    /// <param name="events">The hub the listener subscribes to.</param>
    /// <returns>The listener and the History store behind it.</returns>
    private static (DocHistoryLibraryListener Listener, DocHistoryStore Store) Build(LibraryFileServiceFixture fixture, LibraryEvents events)
    {
        DocHistoryStore store = new(fixture.BoundOptions, NullLogger<DocHistoryStore>.Instance);
        DocHistoryRecorder recorder = new(store, fixture.BoundOptions, NullLogger<DocHistoryRecorder>.Instance, new ConfiguredHumanIdentity("Emre"), new FakeTimeProvider());
        return (new DocHistoryLibraryListener(events, recorder), store);
    }
}
