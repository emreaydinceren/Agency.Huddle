namespace Agency.Huddle.App.Library;

/// <summary>The role of a node in the Library tree structure.</summary>
public enum LibraryNodeRole
{
    /// <summary>A Library Root (Teams, Agents, or pinned).</summary>
    Root,

    /// <summary>A Team folder inside Teams or Agents root.</summary>
    TeamFolder,

    /// <summary>A Project folder inside a Team folder.</summary>
    ProjectFolder,

    /// <summary>An Agent folder inside Agents root.</summary>
    AgentFolder,

    /// <summary>An Agent definition file (<c>.md</c>) inside an Agent folder.</summary>
    AgentDefinition,

    /// <summary>The work directory inside an Agent folder.</summary>
    WorkDir,

    /// <summary>A regular folder.</summary>
    Folder,

    /// <summary>A file.</summary>
    File,
}
