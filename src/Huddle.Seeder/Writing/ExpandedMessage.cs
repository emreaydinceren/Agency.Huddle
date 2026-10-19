using Agency.Huddle.Contracts;

namespace Agency.Huddle.Seeder.Writing;

/// <summary>A Message's final text and the references the app would have stored for it.</summary>
/// <param name="Text">The text with every token expanded.</param>
/// <param name="References">The references in first-seen order, or <see langword="null"/> for none.</param>
internal sealed record ExpandedMessage(string Text, IReadOnlyList<MessageReference>? References);
