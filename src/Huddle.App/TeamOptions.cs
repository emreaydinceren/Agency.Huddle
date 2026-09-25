using Agency.Huddle.App.Acp;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Library;
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
    // everything - see docs/agencyteam/known-limits.md.
    public int AgentMessageBudget { get; set; } = 40;

    public DemoAgentOptions DemoAgent { get; set; } = new();

    public AcpOptions Acp { get; set; } = new();

    public FileChangesOptions FileChanges { get; set; } = new();

    /// <summary>Configuration for the Library feature, per Spec §7: file explorer, document viewer, wikilinks, pinned roots.</summary>
    public LibraryOptions Library { get; set; } = new();

    /// <summary>Configuration for team and teammate folder names, per Spec §7. Shared with the Tasks feature.</summary>
    public TeamsOptions Teams { get; set; } = new();

    /// <summary>Configuration for the Tasks feature.</summary>
    public TasksOptions Tasks { get; set; } = new();
}

public sealed class DemoAgentOptions
{
    public bool Enabled { get; set; } = true;

    // Left null by default rather than pre-populated with ["echo", "alpha"]: the configuration binder
    // appends bound array elements to an already-populated list/array property instead of replacing it,
    // which would silently double up the built-in agents. DemoAgentHost falls back to the documented
    // default (Team-Specifications.md §6.7) when this is null or empty.
    public IReadOnlyList<string>? Names { get; set; }
}