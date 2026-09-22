using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Services;

/// <summary>
/// One Turn's text as it arrives, before it becomes a Message — see <c>docs/agencyteam/language.md</c>
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
/// <param name="ToolTitle">
/// What the Agent is currently doing, if it is mid tool call right now; <see langword="null"/> when it
/// is not.
/// </param>
/// <param name="ToolStatus">
/// The lifecycle state of that tool call; <see langword="null"/> exactly when <paramref name="ToolTitle"/> is.
/// </param>
public sealed record Draft(
    string MessageId,
    string RoomId,
    string AgentId,
    string SenderName,
    string Text,
    string? ToolTitle,
    ToolActivityStatus? ToolStatus);
