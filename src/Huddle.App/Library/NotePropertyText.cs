using System.Text;

namespace Agency.Huddle.App.Library;

/// <summary>The rules of D-11 for property text, which comes from files an Agent may have written: bidirectional controls are removed and a long value is cut before it is drawn.</summary>
internal static class NotePropertyText
{
    /// <summary>A Text value longer than this is cut there and ends with an ellipsis; the source is unchanged.</summary>
    internal const int MaxLength = 2000;

    /// <summary>Removes bidirectional control characters (U+200E, U+200F, U+202A to U+202E, U+2066 to U+2069) and cuts the text at <see cref="MaxLength"/>.</summary>
    /// <param name="text">A key, a value or a list item as read.</param>
    internal static string Prepare(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        StringBuilder builder = new(text.Length);
        foreach (char c in text)
        {
            if (!IsBidiControl(c))
            {
                _ = builder.Append(c);
            }
        }

        if (builder.Length <= MaxLength)
        {
            return builder.ToString();
        }

        // A cut that would leave half of a surrogate pair goes one character earlier.
        int cut = char.IsHighSurrogate(builder[MaxLength - 1]) ? MaxLength - 1 : MaxLength;
        return string.Concat(builder.ToString(0, cut), "…");
    }

    /// <summary>The value as written, as one string: a scalar's text, a list's items joined, or an unsupported entry's source.</summary>
    /// <param name="property">The property.</param>
    internal static string Raw(NoteProperty property)
    {
        ArgumentNullException.ThrowIfNull(property);

        return property.Shape switch
        {
            NotePropertyShape.Scalar => property.Value,
            NotePropertyShape.List => property.Items.Count > 0 ? string.Join(", ", property.Items) : SourceItems(property.Source),
            NotePropertyShape.Nested => property.Source,
            _ => string.Empty,
        };
    }

    /// <summary>The items a list was written with, as plain text: the source's lines without their dashes. It shows a list item the reader skipped, such as an unquoted <c>#</c> item.</summary>
    private static string SourceItems(string source) =>
        string.Join("; ", source.Split('\n').Select(static line => line.Trim()).Select(static line => line.StartsWith("- ", StringComparison.Ordinal) ? line[2..].Trim() : line).Where(static line => line.Length > 0));

    private static bool IsBidiControl(char c) => c is '\u200E' or '\u200F' or (>= '\u202A' and <= '\u202E') or (>= '\u2066' and <= '\u2069');
}
