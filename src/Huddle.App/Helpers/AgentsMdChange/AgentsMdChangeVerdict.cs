using System.Text.Json.Serialization;
using Agency.Huddle.App.AgentsMdChanges;

namespace Agency.Huddle.App.Helpers.AgentsMdChange;

/// <summary>The Helper's answer.</summary>
/// <param name="Meaningful">Whether the edit could change what an Agent does; required, because a missing value must not read as false.</param>
/// <param name="Reason">Why, in a few words.</param>
/// <param name="Changes">Each rule added, changed or removed.</param>
internal sealed record AgentsMdChangeVerdict([property: JsonRequired] bool Meaningful, string? Reason, IReadOnlyList<AgentsMdRuleChange>? Changes);
