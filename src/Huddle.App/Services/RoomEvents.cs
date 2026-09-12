using Agency.Huddle.App.Data;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Services;

public sealed record MessagePostedEvent(
    Room Room, ChatMessage Message, IReadOnlyList<User> Members, IReadOnlyList<User> Mentions);

public sealed class RoomEvents
{
    private readonly ILogger<RoomEvents> logger;

    public RoomEvents(ILogger<RoomEvents> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        this.logger = logger;
    }

    public event Action<MessagePostedEvent>? MessagePosted;

    public event Action? RoomsChanged;

    public void PublishMessagePosted(MessagePostedEvent e)
    {
        if (this.MessagePosted is not { } handlers)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action<MessagePostedEvent>)handler).Invoke(e);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.MessagePosted));
            }
        }
    }

    public void PublishRoomsChanged()
    {
        if (this.RoomsChanged is not { } handlers)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action)handler).Invoke();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.RoomsChanged));
            }
        }
    }
}