namespace Agency.Huddle.App.Library;

/// <summary>The role of a node in the Library tree structure.</summary>
public enum LibraryNodeRole
{
    /// <summary>A Library Root (Teams, Teammates, or pinned).</summary>
    Root,

    /// <summary>A Team folder inside Teams or Teammates root.</summary>
    TeamFolder,

    /// <summary>A Project folder inside a Team folder.</summary>
    ProjectFolder,

    /// <summary>A Teammate folder inside Teammates root.</summary>
    TeammateFolder,

    /// <summary>A Teammate definition file (<c>.md</c>) inside a Teammate folder.</summary>
    TeammateDefinition,

    /// <summary>The work directory inside a Teammate folder.</summary>
    WorkDir,

    /// <summary>A regular folder.</summary>
    Folder,

    /// <summary>A file.</summary>
    File,
}
