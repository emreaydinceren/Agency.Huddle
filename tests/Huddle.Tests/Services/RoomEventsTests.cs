using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Services;

public sealed class RoomEventsTests
{
    /// <summary>
    /// A throwing <see cref="RoomEvents.DraftChanged"/> subscriber must not stop a later subscriber
    /// from being called — the same fan-out guarantee <see cref="RoomEvents.RoomsChanged"/> and the
    /// Message events already give.
    /// </summary>
    [Fact]
    public void PublishDraftChanged_InvokesAllHandlers_EvenIfOneThrows()
    {
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var secondInvokedWithRoomId = string.Empty;
        events.DraftChanged += _ => throw new InvalidOperationException("boom");
        events.DraftChanged += roomId => secondInvokedWithRoomId = roomId;

        var exception = Record.Exception(() => events.PublishDraftChanged("room1"));

        Assert.Null(exception);
        Assert.Equal("room1", secondInvokedWithRoomId);
    }

    [Fact]
    public void Publish_InvokesAllHandlers_EvenIfOneThrows()
    {
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var secondInvoked = false;
        events.RoomsChanged += () => throw new InvalidOperationException("boom");
        events.RoomsChanged += () => secondInvoked = true;

        var exception = Record.Exception((Action)(() => events.PublishRoomsChanged()));

        Assert.Null(exception);
        Assert.True(secondInvoked);
    }

    [Fact]
    public void Publish_WithNoSubscribers_DoesNotThrow()
    {
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);

        var messageException = Record.Exception(() => events.PublishMessagePosted(
            new MessagePostedEvent(
                new Room("r1", "room", DateTimeOffset.UtcNow),
                new ChatMessage("m1", DateTimeOffset.UtcNow, "human", "You", "hi"),
                [],
                [],
                new RoomBudget(0, 40))));
        var roomsException = Record.Exception((Action)(() => events.PublishRoomsChanged()));

        Assert.Null(messageException);
        Assert.Null(roomsException);
    }
}