namespace Agency.Huddle.App.Library;

/// <summary>One tag in a Library Root: its full display path (for example "work/active") and how many distinct notes carry it or a child.</summary>
/// <param name="Text">The tag's display path, in the root's display casing.</param>
/// <param name="NoteCount">The distinct notes in the tag's subtree.</param>
internal sealed record LibraryTag(string Text, int NoteCount);
