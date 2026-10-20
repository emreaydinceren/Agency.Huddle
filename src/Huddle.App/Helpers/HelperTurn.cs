using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.App.Helpers;

/// <summary>What one Helper Turn produced.</summary>
/// <param name="Text">Every message chunk of the Turn, in order; thoughts excluded.</param>
/// <param name="ModelOffered">False when a Model was requested and the session's catalog did not list it; the prompt was then not sent.</param>
/// <param name="ToolCallsRefused">How many tool permission requests the session made and was refused.</param>
/// <param name="Cost">The last cost the adapter reported, when it reports one.</param>
internal sealed record HelperTurn(string Text, bool ModelOffered, int ToolCallsRefused, UsageCost? Cost);
