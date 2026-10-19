using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Services;

/// <summary>
/// One tool call in a Draft's Turn detail, as last reported. A field the Adapter did not repeat on a
/// later update keeps the value it had, because ACP reads an omitted field as unchanged. Public for
/// the same reason as <see cref="Draft"/>: it is a <c>[Parameter]</c> type.
/// </summary>
/// <param name="ToolCallId">The id the Adapter gave the call; the key rows are merged by.</param>
/// <param name="Title">What the Agent is doing, or <see langword="null"/> if it never said.</param>
/// <param name="Status">The call's lifecycle state.</param>
/// <param name="Path">The file the call touches, or <see langword="null"/> when not known.</param>
/// <param name="Line">The 1-based line in <paramref name="Path"/>, or <see langword="null"/> when not known.</param>
/// <param name="Edit">What the call changes, for the Edit preview, or <see langword="null"/> when it is not an edit.</param>
public sealed record ToolCallDetail(
    string ToolCallId,
    string? Title,
    ToolActivityStatus Status,
    string? Path,
    int? Line,
    EditChange? Edit);
