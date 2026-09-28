namespace Agency.Huddle.App.Library;

/// <summary>The kind of Library Root: a Team folder collection, Teammate folder collection, or pinned root.</summary>
public enum LibraryRootKind
{
    /// <summary>Team folders.</summary>
    Teams,

    /// <summary>Teammate folders.</summary>
    Teammates,

    /// <summary>Pinned root.</summary>
    Pinned,
}
