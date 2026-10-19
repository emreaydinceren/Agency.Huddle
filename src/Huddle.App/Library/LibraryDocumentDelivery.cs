namespace Agency.Huddle.App.Library;

/// <summary>How one Library document reaches the model.</summary>
public enum LibraryDocumentDelivery
{
    /// <summary>Today's line: the path, its location and size (and, for an Adapter with no file tools, the inline text).</summary>
    Path,

    /// <summary>The document travels as a Prompt block; its line says it is included with the message.</summary>
    Block,

    /// <summary>An image the model cannot be given: its line says so, so the model does not try to read it.</summary>
    Unavailable,
}
