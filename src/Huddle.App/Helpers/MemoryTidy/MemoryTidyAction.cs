namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>One change the Verdict asks for, as the model wrote it. Everything after the parse works on <see cref="MemoryTidyStep"/>.</summary>
/// <param name="Action"><c>merge</c>, <c>rewrite</c>, <c>describe</c> or <c>drop</c>.</param>
/// <param name="File">The file a rewrite, describe or drop acts on.</param>
/// <param name="Into">The file a merge keeps.</param>
/// <param name="From">The files a merge folds in and removes.</param>
/// <param name="Description">The new description; required for a describe, optional for a merge and a rewrite, unused for a drop.</param>
/// <param name="Text">The whole new body, without frontmatter; only for a merge and a rewrite.</param>
/// <param name="Reason">Why, in a few words: shown in the list, the History and the Feed preview.</param>
internal sealed record MemoryTidyAction(string? Action, string? File, string? Into, IReadOnlyList<string>? From, string? Description, string? Text, string? Reason);
