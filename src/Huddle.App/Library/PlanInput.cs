namespace Agency.Huddle.App.Library;

/// <summary>One document offered to the planner.</summary>
/// <param name="Path">The resolved Library path.</param>
/// <param name="Kind">What the Library says the file is. Only <see cref="LibraryFileKind.Image"/> can be an image block.</param>
/// <param name="FromTrigger">Whether the path was written in the Message that started the Turn (D-2).</param>
/// <param name="InlineText">The document's text, already cut to the inline limit, or <see langword="null"/> when it was not read.</param>
internal sealed record PlanInput(LibraryPath Path, LibraryFileKind Kind, bool FromTrigger, string? InlineText = null);
