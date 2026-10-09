namespace Agency.Huddle.App.Teams;

/// <summary>The one place that spells the Teams data-folder and file names (Spec §Data).</summary>
internal static class TeamPaths
{
    /// <summary>Folder holding the data-defined team types.</summary>
    public const string TeamTypesDir = "TeamTypes";

    /// <summary>Folder holding the skill packs.</summary>
    public const string SkillPacksDir = "SkillPacks";

    /// <summary>The skill catalog file.</summary>
    public const string SkillsCatalogFile = "SkillsCatalog.json";

    /// <summary>A Team's own file.</summary>
    public const string TeamFileName = "_team.md";

    /// <summary>A Team's process document.</summary>
    public const string ProcessFileName = "Process.md";

    /// <summary>A Team's retrospective register.</summary>
    public const string RetroRegisterFileName = "Retro register.md";

    /// <summary>Folder holding retrospective notes.</summary>
    public const string RetrosDir = "_retros";

    /// <summary>File, under the data folder, listing the connected connectors.</summary>
    public const string ConnectorsFileName = "connectors.json";

    /// <summary>Folder, under the data folder, holding the tool-call log.</summary>
    public const string ToolCallLogDir = "logs/tool-calls";
}
