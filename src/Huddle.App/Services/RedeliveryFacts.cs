using Agency.Huddle.App.Data;

namespace Agency.Huddle.App.Services;

/// <summary>
/// The three facts the Reply Gate needs about a re-delivered Message, deliberately not the
/// <see cref="MessagePostedEvent"/> itself. <see cref="RoomEvents.MessageRedelivered"/>'s own doc
/// records that no Blazor component may subscribe to it - a component that handled the envelope could
/// render the Message a second time. Handing the caller the envelope would put that same mistake one
/// careless <c>messages.Add(...)</c> away; handing it only these three facts removes the temptation
/// rather than policing it with a comment and a guard test.
/// </summary>
/// <param name="Members">The Room's Members at the moment of re-delivery.</param>
/// <param name="Mentions">Who the re-delivered Message names, re-parsed fresh so it matches exactly who it woke the first time.</param>
/// <param name="SenderId">The re-delivered Message's original sender.</param>
public sealed record RedeliveryFacts(
    IReadOnlyList<User> Members,
    IReadOnlyList<User> Mentions,
    string SenderId);