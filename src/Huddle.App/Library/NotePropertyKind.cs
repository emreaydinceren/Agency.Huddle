namespace Agency.Huddle.App.Library;

/// <summary>The type a front matter property is shown as: the reference app's eight, plus Huddle's own Unsupported and Mismatch (front matter properties spec, Vocabulary).</summary>
public enum NotePropertyKind
{
    /// <summary>Text; also the type of an empty value.</summary>
    Text,

    /// <summary>A list of strings.</summary>
    List,

    /// <summary>A number, kept as written.</summary>
    Number,

    /// <summary>A true or false value.</summary>
    Checkbox,

    /// <summary>A calendar date, <c>YYYY-MM-DD</c>.</summary>
    Date,

    /// <summary>A date with a time, optionally with an offset.</summary>
    DateTime,

    /// <summary>The list of other names for the note.</summary>
    Aliases,

    /// <summary>The note's tags, read by the tag reader.</summary>
    Tags,

    /// <summary>A nested or unreadable value, shown as its source.</summary>
    Unsupported,

    /// <summary>A value that does not fit the type assigned to its key.</summary>
    Mismatch,
}
