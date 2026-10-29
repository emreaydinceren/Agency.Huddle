namespace Agency.Huddle.Contracts;

/// <summary>The kinds a Message can reference (spec §8.1). An Agent is Mentioned, never referenced.</summary>
public enum ReferenceKind
{
    /// <summary>A Task, keyed by its id.</summary>
    Task,

    /// <summary>A Room, keyed by its id.</summary>
    Room,

    /// <summary>A Team, keyed by its Name.</summary>
    Team,

    /// <summary>A Library file, keyed by its absolute path as written.</summary>
    LibraryFile,
}
