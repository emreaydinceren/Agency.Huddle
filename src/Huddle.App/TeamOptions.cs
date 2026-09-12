using Agency.Huddle.App.Acp;

namespace Agency.Huddle.App;

public sealed class TeamOptions
{
    public const string SectionName = "Team";

    public string DataDir { get; set; } = "App_Data";

    public string PipeName { get; set; } = "team";

    public string HumanName { get; set; } = "You";

    public DemoAgentOptions DemoAgent { get; set; } = new();

    public AcpOptions Acp { get; set; } = new();
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