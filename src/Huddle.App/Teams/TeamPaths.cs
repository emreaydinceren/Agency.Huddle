namespace Agency.Huddle.App.Teams;

/// <summary>The one place that spells the Teams data-folder and file names (Spec §Data).</summary>
internal static class TeamPaths
{
    /// <summary>Folder holding the data-defined team types.</summary>
    public const string TeamTypesDir = "TeamTypes";

    /// <summary>Folder holding the user's hiring pool (<c>hiringpool.json</c> and its personas).</summary>
    public const string HiringPoolDir = "HiringPool";

    /// <summary>The folder where Packs were kept before Skill Sources; only the one-time move reads it now (Skill Packs spec §8.7).</summary>
    public const string SkillPacksDir = "SkillPacks";

    /// <summary>The Human's copy of the old Skills catalog; only the one-time move reads it now (Skill Packs spec §8.7).</summary>
    public const string SkillsCatalogFile = "SkillsCatalog.json";

    /// <summary>A Team's own file.</summary>
    public const string TeamFileName = "_team.md";

    /// <summary>The Human's standing Instructions, at the Teams root, in a Team folder or in an Agent's Work Dir (Instructions and Memory §3.1).</summary>
    public const string InstructionsFileName = "Instructions.md";

    /// <summary>The agent-guide file name, which a memory folder never takes as a topic.</summary>
    public const string AgentsMdFileName = "AGENTS.md";

    /// <summary>A Team's retrospective register.</summary>
    public const string RetroRegisterFileName = "Retro register.md";

    /// <summary>Folder holding retrospective notes.</summary>
    public const string RetrosDir = "_retros";

    /// <summary>File, under the data folder, listing the connected connectors.</summary>
    public const string ConnectorsFileName = "connectors.json";

    /// <summary>Folder, under the data folder, holding the tool-call log.</summary>
    public const string ToolCallLogDir = "logs/tool-calls";
}
