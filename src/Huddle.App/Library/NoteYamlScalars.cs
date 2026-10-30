using System.Globalization;
using System.Text;

namespace Agency.Huddle.App.Library;

/// <summary>Reads the YAML scalar forms a note property can hold: quoted strings, comments and the plain-scalar inference of D-2 step 3.</summary>
internal static class NoteYamlScalars
{
    /// <summary>Reads a quoted scalar starting at <paramref name="start"/>; <see langword="false"/> when its closing quote is missing.</summary>
    /// <param name="text">The text holding the scalar.</param>
    /// <param name="start">The offset of the opening quote.</param>
    /// <param name="value">The decoded string.</param>
    /// <param name="endExclusive">The offset just past the closing quote.</param>
    internal static bool TryReadQuoted(string text, int start, out string value, out int endExclusive)
    {
        char quote = text[start];
        StringBuilder builder = new();
        int i = start + 1;
        while (i < text.Length)
        {
            char c = text[i];
            if (quote == '"' && c == '\\' && i + 1 < text.Length)
            {
                i += AppendEscape(text, i + 1, builder) + 1;
                continue;
            }

            if (c == quote)
            {
                if (quote == '\'' && i + 1 < text.Length && text[i + 1] == '\'')
                {
                    _ = builder.Append('\'');
                    i += 2;
                    continue;
                }

                value = builder.ToString();
                endExclusive = i + 1;
                return true;
            }

            _ = builder.Append(c);
            i++;
        }

        value = string.Empty;
        endExclusive = text.Length;
        return false;
    }

    /// <summary>Appends the character a double-quoted escape stands for, starting at the character after the backslash.</summary>
    /// <returns>How many characters after the backslash the escape used.</returns>
    private static int AppendEscape(string text, int at, StringBuilder builder)
    {
        char escaped = text[at];
        if (escaped == 'u' && at + 4 < text.Length && ushort.TryParse(text.AsSpan(at + 1, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort code))
        {
            _ = builder.Append((char)code);
            return 5;
        }

        _ = builder.Append(escaped switch
        {
            'n' => '\n',
            't' => '\t',
            'r' => '\r',
            '0' => '\0',
            _ => escaped,
        });
        return 1;
    }

    /// <summary>Drops a YAML comment: an unquoted <c>#</c> that follows whitespace.</summary>
    /// <param name="text">A plain scalar's text.</param>
    internal static string StripComment(string text)
    {
        for (int i = 1; i < text.Length; i++)
        {
            if (text[i] == '#' && text[i - 1] is ' ' or '\t')
            {
                return text[..i];
            }
        }

        return text;
    }

    /// <summary>Whether <paramref name="text"/> holds a map key: a colon followed by a space or the end.</summary>
    /// <param name="text">A plain scalar's text.</param>
    internal static bool HasKeyColon(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == ':' && (i + 1 == text.Length || text[i + 1] is ' ' or '\t'))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether a plain scalar is a YAML null.</summary>
    /// <param name="value">The scalar text.</param>
    internal static bool IsNull(string value) => value is "" or "~" or "null" or "Null" or "NULL";

    /// <summary>D-2 step 3 for one scalar: the type its text implies. A quoted scalar is a string, so it is always Text.</summary>
    /// <param name="value">The scalar text without quotes.</param>
    /// <param name="quoted">Whether the scalar was written in quotes.</param>
    internal static (NotePropertyKind Kind, NotePropertyShape Shape) Infer(string value, bool quoted)
    {
        if (quoted)
        {
            return (NotePropertyKind.Text, value.Length == 0 ? NotePropertyShape.Empty : NotePropertyShape.Scalar);
        }

        if (IsNull(value))
        {
            return (NotePropertyKind.Text, NotePropertyShape.Empty);
        }

        NotePropertyKind kind = KindOfPlain(value);
        return (kind, NotePropertyShape.Scalar);
    }

    /// <summary>The type an unquoted, non-null scalar implies.</summary>
    private static NotePropertyKind KindOfPlain(string value)
    {
        if (NotePropertyValues.TryParseBoolean(value, out _))
        {
            return NotePropertyKind.Checkbox;
        }

        if (NotePropertyValues.IsNumber(value))
        {
            return NotePropertyKind.Number;
        }

        if (NotePropertyValues.TryParseDate(value, out _))
        {
            return NotePropertyKind.Date;
        }

        return NotePropertyValues.TryParseDateTime(value, out _, out _) ? NotePropertyKind.DateTime : NotePropertyKind.Text;
    }
}
