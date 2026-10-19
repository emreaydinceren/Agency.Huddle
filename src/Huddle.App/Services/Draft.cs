using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Services;

/// <summary>
/// One Turn's text as it arrives, before it becomes a Message — see <c>docs/engineering/language.md</c>
/// for why a Draft is not a Message: a Message is one persisted unit of text, and a Draft is never
/// written to the Transcript. Public even though the store that produces it, <see cref="Drafts"/>, is
/// not: a later task passes a <see cref="Draft"/> as a Blazor component <c>[Parameter]</c>, and Razor
/// generates component classes as <see langword="public"/>, so an internal parameter type would fail
/// to build with <c>CS0053</c> — the same reasoning that made <c>PromptTiming</c> and <c>PromptIssue</c>
/// public while the stores behind them stayed internal.
/// </summary>
/// <param name="MessageId">
/// The id the Message will have once the Turn completes and is posted — the key <see cref="Drafts"/>
/// keeps this Draft under.
/// </param>
/// <param name="RoomId">
/// The Room this Draft is shown in. Carried here, not used as the key, because two Agents can stream
/// into one Room at once — a Group Room where both were Mentioned — and a Draft keyed by Room id
/// would silently drop one of them.
/// </param>
/// <param name="AgentId">The Agent whose Turn this is.</param>
/// <param name="SenderName">The Agent's display name, so the Room view needs no extra lookup to render it.</param>
/// <param name="Text">The text that has arrived so far.</param>
/// <param name="ToolCalls">
/// The Turn's most recent tool calls, oldest first and at most <see cref="Drafts.MaxToolCalls"/> of
/// them: the Turn detail a Room shows under the text. Empty when the Turn has made no tool call.
/// </param>
public sealed record Draft(
    string MessageId,
    string RoomId,
    string AgentId,
    string SenderName,
    string Text,
    IReadOnlyList<ToolCallDetail> ToolCalls)
{
    /// <summary>
    /// The title of the newest tool call, or <see langword="null"/> when the Turn has made none or the
    /// newest one has no title yet.
    /// </summary>
    public string? ToolTitle => this.ToolCalls.Count > 0 ? this.ToolCalls[^1].Title : null;

    /// <summary>The status of the newest tool call, or <see langword="null"/> when the Turn has made none.</summary>
    public ToolActivityStatus? ToolStatus => this.ToolCalls.Count > 0 ? this.ToolCalls[^1].Status : null;
}
