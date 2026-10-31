using Agency.Huddle.App.Data;
using Agency.Huddle.App.References;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.References;

/// <summary>
/// A Room reference pasted into a Composer is the same text the picker inserts, so it is snapshotted the same way
/// at send (spec §6.1, AT-T20). A bare absolute Room URL is not a reference (V2, spec §6.1).
/// </summary>
public sealed class PastedReferenceTests
{
    /// <summary>A pasted Copy link text (with the trailing space the picker adds) is snapshotted exactly as the reference it was copied from.</summary>
    [Fact]
    public async Task PastedRoomReference_SnapshotEqualsPickedOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        Room room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, fixture.Nova.Id], ct);
        ChatService chat = fixture.Chat(fixture.Snapshotter(), new RoomEvents(NullLogger<RoomEvents>.Instance));
        MessageReference expected = new(ReferenceKind.Room, room.Id, ReferenceText.RoomPlainLabelFor(room.Name, room.Team, room.IsTeamRoom));

        ChatMessage message = await chat.PostAsync(room.Id, KnownIds.Human, RoomLinkText.For(room) + " ", ct: ct);

        Assert.Equal([expected], message.References);
    }

    /// <summary>The snapshot keeps the Room's plain label, with no Markdown backslashes, so a chip can draw it (finding 7a).</summary>
    [Fact]
    public async Task RoomWithMarkdownSpecials_SnapshotLabelIsPlain()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        Room room = await fixture.Directory.CreateRoomAsync("Q3_plan", [KnownIds.Human, fixture.Nova.Id], ct);
        ChatService chat = fixture.Chat(fixture.Snapshotter(), new RoomEvents(NullLogger<RoomEvents>.Instance));

        ChatMessage message = await chat.PostAsync(room.Id, KnownIds.Human, RoomLinkText.For(room), ct: ct);

        Assert.Equal("Q3_plan", Assert.Single(message.References ?? []).Label);
    }

    /// <summary>A bare absolute Room URL pasted from the address bar stays plain text: no snapshot is taken.</summary>
    [Fact]
    public async Task BareAbsoluteRoomUrl_IsNotAReference()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        Room room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, fixture.Nova.Id], ct);
        ChatService chat = fixture.Chat(fixture.Snapshotter(), new RoomEvents(NullLogger<RoomEvents>.Instance));

        ChatMessage message = await chat.PostAsync(room.Id, KnownIds.Human, $"http://localhost:5100/rooms/{room.Id}", ct: ct);

        Assert.Null(message.References);
    }
}
