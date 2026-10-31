using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Services;

/// <summary>
/// Pins the <c>last-catch-up</c> cleanup of <see cref="ChatService.DeleteRoomAsync"/> (LR-E11): deleting
/// a Room removes its files under every Name, archiving it removes nothing.
/// </summary>
public sealed class ChatServiceDeleteRoomTests
{
    /// <summary>Deleting a Room removes its reread text and note under every Name and leaves the other Rooms' files.</summary>
    [Fact]
    public async Task DeleteRoom_RemovesItsLastCatchUpFilesUnderEveryName_AndLeavesOtherRooms()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        Room doomed = await directory.CreateRoomAsync("doomed", [KnownIds.Human], ct);
        Room kept = await directory.CreateRoomAsync("kept", [KnownIds.Human], ct);
        LastCatchUpStore store = new(dir.Options(), NullLogger<LastCatchUpStore>.Instance);
        foreach (string name in new[] { "Nova", "Echo" })
        {
            store.Write(name, doomed.Id, "doomed text");
            store.WriteNote(name, new FreshStartNote(doomed.Id, "reason", 3, 10, false));
            store.Write(name, kept.Id, "kept text");
            store.WriteNote(name, new FreshStartNote(kept.Id, "reason", 3, 10, false));
        }

        ChatService service = CreateService(dir, directory, store);

        await service.DeleteRoomAsync(doomed.Id, ct);

        Assert.Null(store.Read("Nova", doomed.Id));
        Assert.Null(store.Read("Echo", doomed.Id));
        Assert.Empty(store.ReadNotes(doomed.Id));
        Assert.Equal("kept text", store.Read("Nova", kept.Id));
        Assert.Equal("kept text", store.Read("Echo", kept.Id));
        Assert.Equal(2, store.ReadNotes(kept.Id).Count);
    }

    /// <summary>Archiving a Room is a display filter only: its reread text and note stay.</summary>
    [Fact]
    public async Task ArchiveRoom_RemovesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        Room room = await directory.CreateRoomAsync("archived", [KnownIds.Human], ct);
        LastCatchUpStore store = new(dir.Options(), NullLogger<LastCatchUpStore>.Instance);
        store.Write("Nova", room.Id, "text");
        store.WriteNote("Nova", new FreshStartNote(room.Id, "reason", 3, 10, false));
        ChatService service = CreateService(dir, directory, store);

        await service.SetRoomArchivedAsync(room.Id, true, ct);

        Assert.Equal("text", store.Read("Nova", room.Id));
        Assert.Single(store.ReadNotes(room.Id));
    }

    /// <summary>A <see cref="FreshStartNotes"/> whose cache is already filled forgets a Room's note when the store removes the Room, on the same instance.</summary>
    [Fact]
    public void FreshStartNotes_AfterStoreRemoveRoom_ForIsEmptyOnTheSameInstance()
    {
        using TempDataDir dir = new();
        LastCatchUpStore store = new(dir.Options(), NullLogger<LastCatchUpStore>.Instance);
        FreshStartNotes notes = new(store);
        notes.Set("room-1", "Nova", new FreshStartNote("room-1", "reason", 3, 10, false));
        Assert.Single(notes.For("room-1"));

        store.RemoveRoom("room-1");

        Assert.Empty(notes.For("room-1"));
    }

    /// <summary>A <see cref="FreshStartNotes"/> whose cache is already filled no longer reports the old Name's note after the store renames the Name.</summary>
    [Fact]
    public void FreshStartNotes_AfterStoreRename_OldNameIsGoneFromFor()
    {
        using TempDataDir dir = new();
        LastCatchUpStore store = new(dir.Options(), NullLogger<LastCatchUpStore>.Instance);
        FreshStartNotes notes = new(store);
        notes.Set("room-1", "Nova", new FreshStartNote("room-1", "reason", 3, 10, false));
        Assert.Single(notes.For("room-1"));

        store.Rename("Nova", "Novaprime");

        Assert.DoesNotContain(notes.For("room-1"), n => n.AgentName == "Nova");
    }

    /// <summary>Builds a <see cref="ChatService"/> over the temp Data Dir with <paramref name="store"/> wired in as its last-catch-up store.</summary>
    private static ChatService CreateService(TempDataDir dir, SqliteTeamDirectory directory, LastCatchUpStore store)
    {
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        FakeMentionAliasSource aliasSource = new() { Aliases = [] };
        ChatService service = new(
            directory,
            dir.CreateChatStore(),
            events,
            aliasSource,
            TestOptions.Create(static _ => { }),
            new ProposalStore(events),
            new QuestionStore(events),
            NullLogger<ChatService>.Instance)
        {
            LastCatchUp = store,
        };
        return service;
    }
}
