namespace Agency.Huddle.App.Library;

/// <summary>
/// The one place that decides what a Library note's frontmatter is (Spec §6.17): a first line of exactly
/// <c>---</c> (after an optional BOM), closed by the next line that is exactly <c>---</c> or <c>...</c>. A block that never
/// closes is not frontmatter. The tag pass, <see cref="NoteTagReader"/> and the Properties block all split here, so they
/// always agree on where the block ends.
/// </summary>
internal static class NoteFrontmatter
{
    /// <summary>Splits <paramref name="text"/> into its frontmatter block and the offset where the body starts.</summary>
    /// <param name="text">The note's decoded text.</param>
    /// <returns>The lines between the delimiters (the last line keeps its line break) and the body's start offset; a <see langword="null"/> block and offset 0 when there is no closed block.</returns>
    internal static (string? Block, int BodyStart) Split(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        int start = text.Length > 0 && text[0] == '﻿' ? 1 : 0;
        if (!TryReadLine(text, start, out int firstEnd, out int cursor) || !IsMarker(text, start, firstEnd, "---"))
        {
            return (null, 0);
        }

        int blockStart = cursor;
        while (TryReadLine(text, cursor, out int lineEnd, out int next))
        {
            if (IsMarker(text, cursor, lineEnd, "---") || IsMarker(text, cursor, lineEnd, "..."))
            {
                return (text[blockStart..cursor], next);
            }

            cursor = next;
        }

        return (null, 0);
    }

    private static bool IsMarker(string text, int start, int end, string marker) =>
        text.AsSpan(start, end - start).Equals(marker, StringComparison.Ordinal);

    /// <summary>Reads one line starting at <paramref name="position"/>; a line break is <c>\r\n</c>, <c>\n</c> or a lone <c>\r</c>.</summary>
    private static bool TryReadLine(string text, int position, out int contentEnd, out int next)
    {
        contentEnd = position;
        next = position;
        if (position >= text.Length)
        {
            return false;
        }

        int index = position;
        while (index < text.Length && text[index] != '\n' && text[index] != '\r')
        {
            index++;
        }

        contentEnd = index;
        if (index < text.Length)
        {
            index += text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n' ? 2 : 1;
        }

        next = index;
        return true;
    }
}
