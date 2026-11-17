using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>One Message of a Folded Turn (Turn Queue spec, section 6.2).</summary>
/// <param name="Id">The Message's id.</param>
/// <param name="SenderName">The sender's name.</param>
/// <param name="Text">The Message's text.</param>
/// <param name="References">The references the Message carries, or <see langword="null"/> when there are none.</param>
internal sealed record FoldedMessage(string Id, string SenderName, string Text, IReadOnlyList<MessageReference>? References);
