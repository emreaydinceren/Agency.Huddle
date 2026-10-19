namespace Agency.Huddle.Contracts;

/// <summary>
/// One tool call the Agent is making during a Turn, shown to the Human while it happens and never
/// persisted.
/// </summary>
/// <remarks>
/// It carries no raw input or output JSON, because neither is rendered, and no tool-kind, because
/// <paramref name="Title"/> is what a reader needs. This type deliberately does not reuse the names
/// <c>ToolCallStarted</c>/<c>ToolCallUpdated</c>: those already exist in
/// <c>Agency.Huddle.Acp.Abstractions</c>, and the one file that bridges the two namespaces would
/// otherwise have two types of the same name in scope.
/// </remarks>
/// <param name="RoomId">The Room the Turn making this call is running in.</param>
/// <param name="MessageId">The Message the eventual reply, if any, will carry this activity alongside.</param>
/// <param name="ToolCallId">The id of the tool call this activity reports on.</param>
/// <param name="Title">A human-readable label for the call, or <see langword="null"/> if none is available yet.</param>
/// <param name="Status">The call's current lifecycle state.</param>
/// <param name="Path">The file the call touches, or <see langword="null"/> when not known or not sent on this update. Omitted from the wire when null.</param>
/// <param name="Line">The 1-based line in <paramref name="Path"/>, or <see langword="null"/> when not known or not sent on this update. Omitted from the wire when null.</param>
/// <param name="Edit">What the call changes, for the Edit preview, or <see langword="null"/> when not an edit or not sent on this update. Omitted from the wire when null.</param>
public sealed record ToolActivity(
    string RoomId,
    string MessageId,
    string ToolCallId,
    string? Title,
    ToolActivityStatus Status,
    string? Path = null,
    int? Line = null,
    EditChange? Edit = null) : ProtocolMessage;