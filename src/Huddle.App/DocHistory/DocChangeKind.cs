namespace Agency.Huddle.App.DocHistory;

/// <summary>What a History entry says its author did to the document (DocHistory spec §6.1).</summary>
public enum DocChangeKind
{
    /// <summary>The author wrote the document.</summary>
    Created,

    /// <summary>The author changed the document.</summary>
    Edited,

    /// <summary>The author checked the document; it may describe no change.</summary>
    Reviewed,

    /// <summary>The author disagrees with the document; it may describe no change.</summary>
    Objected,
}
