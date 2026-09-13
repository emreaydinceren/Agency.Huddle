using Agency.Huddle.App.Data;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Services;

public sealed record MessagePostedEvent(
    Room Room,
    ChatMessage Message,
    IReadOnlyList<User> Members,
    IReadOnlyList<User> Mentions,
    RoomBudget Budget);

public sealed class RoomEvents
{
    private readonly ILogger<RoomEvents> logger;

    public RoomEvents(ILogger<RoomEvents> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        this.logger = logger;
    }

    public event Action<MessagePostedEvent>? MessagePosted;

    /// <summary>
    /// A Message already posted and already on screen, being delivered to the Agents again because
    /// the Human extended the Room's Budget. <c>AgentGateway</c> is the only subscriber, and must
    /// stay so: a Blazor component that handled this would render the Message a second time.
    /// </summary>
    public event Action<MessagePostedEvent>? MessageRedelivered;

    public event Action? RoomsChanged;

    public void PublishMessagePosted(MessagePostedEvent e)
    {
        this.Publish(this.MessagePosted, e, nameof(this.MessagePosted));
    }

    public void PublishMessageRedelivered(MessagePostedEvent e)
    {
        this.Publish(this.MessageRedelivered, e, nameof(this.MessageRedelivered));
    }

    private void Publish(Action<MessagePostedEvent>? handlers, MessagePostedEvent e, string eventName)
    {
        if (handlers is null)
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
                this.logger.LogError(ex, "A {Event} handler threw and was skipped.", eventName);
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