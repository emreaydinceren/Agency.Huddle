using System.Text.Json.Serialization;

namespace Agency.Huddle.App.Helpers.NeedsHuman;

/// <summary>The Needs Human Helper's answer.</summary>
/// <param name="NeedsHuman">Whether the Task should wait for the Human's acceptance. Required: a missing answer is not a quiet "no".</param>
/// <param name="Reason">Why, in a few words; shown in the Change log.</param>
/// <param name="Action">Optional: the one irreversible step the Human is accepting, when the Task states one, such as "Merge feat/x into main". It becomes the Task's Action.</param>
internal sealed record NeedsHumanVerdict([property: JsonRequired] bool NeedsHuman, string? Reason, string? Action = null);
