namespace Agency.Huddle.App.Library;

/// <summary>How a property's value was written, which decides what types it can fit (D-3).</summary>
public enum NotePropertyShape
{
    /// <summary>Nothing, <c>~</c> or <c>null</c>.</summary>
    Empty,

    /// <summary>One scalar: its text is <see cref="NoteProperty.Value"/>.</summary>
    Scalar,

    /// <summary>A list of scalars: the strings are <see cref="NoteProperty.Items"/>.</summary>
    List,

    /// <summary>A map, a nested list or an entry that could not be read: only <see cref="NoteProperty.Source"/> is kept.</summary>
    Nested,
}
