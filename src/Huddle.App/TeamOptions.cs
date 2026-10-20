using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.References;
using Agency.Huddle.App.Search;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App;

public sealed class TeamOptions
{
    public const string SectionName = "Team";

    public string DataDir { get; set; } = "App_Data";

    public string PipeName { get; set; } = "team";

    public string HumanName { get; set; } = "You";

    // The per-Room Budget: how many agent-authored Messages one Room may take before a Human speaks
    // there again. Any Human Message in the Room resets it, so this caps one unattended run rather
    // than the Room, and the Human can extend it a Budget at a time from the Room view.
    //
    // Counted in Messages rather than Turns because one Turn can post several times through
    // mcp__team__post_message. It lives here rather than under Acp: ChatService enforces it, and it
    // applies to every agent-authored Message - a demo agent's and a third-party pipe client's
    // included - not only to a Persona running when Team:Acp:Enabled is true.
    //
    // Zero or less disables the cap, which is the only way back to the behaviour ADR-0004 recorded
    // as "deliberately no runaway-loop guard". In memory and per Room, so a restart un-pauses
    // everything - see docs/engineering/known-limits.md.
    public int AgentMessageBudget { get; set; } = 40;

    public DemoAgentOptions DemoAgent { get; set; } = new();

    public AcpOptions Acp { get; set; } = new();

    public FileChangesOptions FileChanges { get; set; } = new();

    /// <summary>Configuration for the Library feature, per Spec §7: file explorer, document viewer, wikilinks, pinned roots.</summary>
    public LibraryOptions Library { get; set; } = new();

    /// <summary>Configuration for a Library document's History, bound from <c>Team:DocHistory</c> per DocHistory spec §6.9.</summary>
    public DocHistoryOptions DocHistory { get; set; } = new();

    /// <summary>Configuration for the Feed, bound from <c>Team:Feed</c> per Feed spec §6.11.</summary>
    public FeedOptions Feed { get; set; } = new();

    /// <summary>Configuration for team and teammate folder names, per Spec §7. Shared with the Tasks feature.</summary>
    public TeamsOptions Teams { get; set; } = new();

    /// <summary>Configuration for the Tasks feature.</summary>
    public TasksOptions Tasks { get; set; } = new();

    /// <summary>Configuration for the Avatar Library, per Spec §6.1. Unset means no library.</summary>
    public AvatarLibraryOptions AvatarLibrary { get; set; } = new();

    /// <summary>Configuration for Global Search, bound from <c>Team:Search</c> per spec §10. Search reads nothing from <c>Team:Library:MaxIndexedFiles</c>.</summary>
    public SearchOptions Search { get; set; } = new();

    /// <summary>Configuration for the Referenced items header of an Agent's Turn, bound from <c>Team:References</c> per spec §10.</summary>
    public ReferencesOptions References { get; set; } = new();

    /// <summary>The Skill Sources folder (Skill Packs spec §8.7); empty means <c>{DataDir}/SkillSources</c>. A folder set on
    /// Settings › Skills › Sources (<c>skill-sources.json</c>) wins over this key.</summary>
    public string SkillSourcesDir { get; set; } = string.Empty;

    /// <summary>The name per-computer paths are matched against; empty means <see cref="Environment.MachineName"/>.</summary>
    public string ComputerName { get; set; } = string.Empty;

    /// <summary>Where this computer's copy of the skills repository is. Read once, by the move; empty means the move adds
    /// no rows for the two Packs Huddle used to ship.</summary>
    public string SkillsRepositoryDir { get; set; } = string.Empty;

    /// <summary>The <c>huddle-packs</c> program <c>InstallSkillPack.md</c> gives for validating; empty means
    /// <c>huddle-packs.exe</c> beside the app, else <c>dotnet huddle-packs.dll</c>.</summary>
    public string HuddlePacksPath { get; set; } = string.Empty;
}