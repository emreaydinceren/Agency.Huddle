using System.Text.Json;

namespace Agency.Huddle.App.Library;

/// <summary>
/// The one place that decides what a Library note's frontmatter is (Spec §6.17): a first line of exactly
/// <c>---</c> (after an optional BOM), closed by the next line that is exactly <c>---</c> or <c>...</c>, or a JSON object that
/// starts the note (front matter properties spec D-9). A block that never closes is not frontmatter. The tag pass,
/// <see cref="NoteTagReader"/> and the Properties block all split here, so they
/// always agree on where the block ends.
/// </summary>
internal static class NoteFrontmatter
{
    /// <summary>Splits <paramref name="text"/> into its frontmatter block and the offset where the body starts.</summary>
    /// <param name="text">The note's decoded text.</param>
    /// <returns>The lines between the delimiters (the last line keeps its line break) and the body's start offset; a <see langword="null"/> block and offset 0 when there is no closed block.</returns>
    internal static (string? Block, int BodyStart) Split(string text)
    {
        NoteFrontmatterBlock located = Locate(text);
        return (located.Text, located.BodyStart);
    }

    /// <summary>
    /// Finds the note's front matter in either spelling (D-9): the <c>---</c> block, or a JSON object that starts the note, parses as one
    /// object and is closed before the body, with nothing else on its closing line.
    /// </summary>
    /// <param name="text">The note's decoded text.</param>
    /// <returns>The block's text, the offset where the body starts and the form found; a <see langword="null"/> text and offset 0 when there is none.</returns>
    internal static NoteFrontmatterBlock Locate(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        int start = text.Length > 0 && text[0] == '﻿' ? 1 : 0;
        if (start < text.Length && text[start] == '{')
        {
            return LocateJson(text, start);
        }

        return LocateYaml(text, start);
    }

    /// <summary>The <c>---</c> form: a first line of exactly <c>---</c>, closed by the next marker line.</summary>
    private static NoteFrontmatterBlock LocateYaml(string text, int start)
    {
        NoteFrontmatterBlock none = new(null, 0, NoteFrontmatterForm.Yaml);
        if (!TryReadLine(text, start, out int firstEnd, out int cursor) || !IsMarker(text, start, firstEnd, "---"))
        {
            return none;
        }

        int blockStart = cursor;
        while (TryReadLine(text, cursor, out int lineEnd, out int next))
        {
            if (IsMarker(text, cursor, lineEnd, "---") || IsMarker(text, cursor, lineEnd, "..."))
            {
                return new NoteFrontmatterBlock(text[blockStart..cursor], next, NoteFrontmatterForm.Yaml);
            }

            cursor = next;
        }

        return none;
    }

    /// <summary>The JSON form (D-9): the leading object, when it is valid JSON and shares its closing line with nothing but whitespace.</summary>
    private static NoteFrontmatterBlock LocateJson(string text, int start)
    {
        NoteFrontmatterBlock none = new(null, 0, NoteFrontmatterForm.Json);
        int end = FindObjectEnd(text, start);
        if (end < 0)
        {
            return none;
        }

        string candidate = text[start..end];
        try
        {
            using JsonDocument document = JsonDocument.Parse(candidate);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return none;
            }
        }
        catch (JsonException)
        {
            return none;
        }

        int lineEnd = end;
        while (lineEnd < text.Length && text[lineEnd] is not ('\n' or '\r'))
        {
            if (!char.IsWhiteSpace(text[lineEnd]))
            {
                return none;
            }

            lineEnd++;
        }

        _ = TryReadLine(text, end, out _, out int next);
        return new NoteFrontmatterBlock(candidate, next == end ? text.Length : next, NoteFrontmatterForm.Json);
    }

    /// <summary>The offset just past the bracket that closes the structure opened at <paramref name="start"/>, or -1 when it never closes.</summary>
    private static int FindObjectEnd(string text, int start)
    {
        int depth = 0;
        bool inString = false;
        bool escaped = false;
        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (c == '"')
            {
                inString = true;
            }
            else if (c is '{' or '[')
            {
                depth++;
            }
            else if (c is '}' or ']')
            {
                depth--;
                if (depth <= 0)
                {
                    return depth == 0 ? i + 1 : -1;
                }
            }
        }

        return -1;
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
