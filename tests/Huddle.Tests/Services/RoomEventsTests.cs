using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Services;

public sealed class RoomEventsTests
{
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