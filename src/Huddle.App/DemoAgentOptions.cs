namespace Agency.Huddle.App;

public sealed class DemoAgentOptions
{
    public bool Enabled { get; set; }

    // Left null by default rather than pre-populated with ["echo", "alpha"]: the configuration binder
    // appends bound array elements to an already-populated list/array property instead of replacing it,
    // which would silently double up the built-in agents. DemoAgentHost falls back to the documented
    // default (Team-Specifications.md §6.7) when this is null or empty.
    public IReadOnlyList<string>? Names { get; set; }
}