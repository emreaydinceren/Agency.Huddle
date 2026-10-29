namespace Agency.Huddle.App.Services;

/// <summary>
/// The handles <see cref="MarkdownRenderer"/> draws as Mention pills in one Room's Message text: an <c>@</c>
/// followed by one of these, matched the way <see cref="MentionParser"/> matches it (longest first,
/// <see cref="StringComparison.OrdinalIgnoreCase"/>, ending at a word boundary), is wrapped in a
/// <c>mention</c> span.
/// </summary>
/// <param name="Handles">The Names and Aliases of the Room's Agents that are Members.</param>
/// <param name="OwnHandles">The Human's own Name and Alias; a Mention of one also carries <c>mention--me</c>.</param>
public sealed record MentionMarks(IReadOnlyList<string> Handles, IReadOnlyList<string> OwnHandles);