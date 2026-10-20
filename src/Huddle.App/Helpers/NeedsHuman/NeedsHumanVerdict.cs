using System.Text.Json.Serialization;

namespace Agency.Huddle.App.Helpers.NeedsHuman;

/// <summary>The Needs Human Helper's answer.</summary>
/// <param name="NeedsHuman">Whether the Task should wait for the Human's acceptance. Required: a missing answer is not a quiet "no".</param>
/// <param name="Reason">Why, in a few words; shown in the Change log.</param>
internal sealed record NeedsHumanVerdict([property: JsonRequired] bool NeedsHuman, string? Reason);
