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
