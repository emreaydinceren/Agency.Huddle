using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Agents;

namespace Agency.Huddle.Tests.Uploads;

/// <summary>Deleting a Room sends its upload folders to the Recycle Bin; archiving keeps them (ChatUploads spec 6.9).</summary>
public sealed class RoomDeleteUploadsTests
{
    private static ChatService CreateChat(UploadServiceHarness harness)
    {
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        FileChatStore store = new(harness.Options, NullLogger<FileChatStore>.Instance);
        return new ChatService(harness.Directory, store, events, new FakeMentionAliasSource(), harness.Options, new ProposalStore(events), new QuestionStore(events), NullLogger<ChatService>.Instance)
        {
            UploadFolders = harness.Folders,
        };
    }

    private static string Seed(string folder, string name = "a.txt")
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, name), "x");
        return folder;
    }

    /// <summary>A root Room's <c>Uploads/&lt;roomId&gt;</c> folder is recycled and the Room is gone.</summary>
    [Fact]
    public async Task DeleteRoomAsync_RootRoom_RecyclesUploadsFolder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], ct);
        string folder = Seed(Path.Combine(harness.DataDir, "Raw", room.Id));

        await CreateChat(harness).DeleteRoomAsync(room.Id, ct);

        Assert.Equal([folder], harness.RecycleBin.Sent);
        Assert.Null(await harness.Directory.GetRoomAsync(room.Id, ct));
    }

    /// <summary>A Team Room's root and Team upload folders are both recycled.</summary>
    [Fact]
    public async Task DeleteRoomAsync_TeamRoom_RecyclesBothFolders()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], team: "Marketing", ct: ct);
        string rootFolder = Seed(Path.Combine(harness.DataDir, "Raw", room.Id));
        string teamFolder = Seed(Path.Combine(harness.DataDir, "Teams", "Marketing", "raw", room.Id));

        await CreateChat(harness).DeleteRoomAsync(room.Id, ct);

        Assert.Equal([rootFolder, teamFolder], harness.RecycleBin.Sent);
    }

    /// <summary>A Room with no upload folders deletes without touching the Recycle Bin.</summary>
    [Fact]
    public async Task DeleteRoomAsync_NoUploadFolders_RecyclesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], team: "Marketing", ct: ct);

        await CreateChat(harness).DeleteRoomAsync(room.Id, ct);

        Assert.Empty(harness.RecycleBin.Sent);
        Assert.Null(await harness.Directory.GetRoomAsync(room.Id, ct));
    }

    /// <summary>A Recycle Bin refusal is logged and never fails the delete.</summary>
    [Fact]
    public async Task DeleteRoomAsync_RecycleFails_StillDeletesTheRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], ct);
        _ = Seed(Path.Combine(harness.DataDir, "Raw", room.Id));
        harness.RecycleBin.FailureError = "No Recycle Bin here.";

        await CreateChat(harness).DeleteRoomAsync(room.Id, ct);

        Assert.Null(await harness.Directory.GetRoomAsync(room.Id, ct));
        Assert.True(Directory.Exists(Path.Combine(harness.DataDir, "Raw", room.Id)));
    }

    /// <summary>Archiving a Room leaves its upload folder alone.</summary>
    [Fact]
    public async Task SetRoomArchivedAsync_Archive_KeepsUploads()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], ct);
        string folder = Seed(Path.Combine(harness.DataDir, "Raw", room.Id));

        _ = await CreateChat(harness).SetRoomArchivedAsync(room.Id, archived: true, ct);

        Assert.Empty(harness.RecycleBin.Sent);
        Assert.True(File.Exists(Path.Combine(folder, "a.txt")));
    }

    /// <summary>The count covers the files of both folders, nested ones included, and is 0 when nothing was uploaded.</summary>
    [Fact]
    public async Task CountRoomUploads_CountsFilesInBothFolders()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], team: "Marketing", ct: ct);
        ChatService chat = CreateChat(harness);
        Assert.Equal(0, chat.CountRoomUploads(room));

        _ = Seed(Path.Combine(harness.DataDir, "Raw", room.Id));
        _ = Seed(Path.Combine(harness.DataDir, "Raw", room.Id), "b.txt");
        _ = Seed(Path.Combine(harness.DataDir, "Teams", "Marketing", "raw", room.Id));

        Assert.Equal(3, chat.CountRoomUploads(room));
    }
}
