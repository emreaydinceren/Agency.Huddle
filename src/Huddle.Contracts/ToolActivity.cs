namespace Agency.Huddle.Contracts;

/// <summary>
/// One tool call the Agent is making during a Turn, shown to the Human while it happens and never
/// persisted.
/// </summary>
/// <remarks>
/// It carries no tool-kind, because <paramref name="Title"/> is what a reader needs, and no complete
/// raw input or output: <paramref name="Arguments"/> and <paramref name="Result"/> are short, clipped
/// summaries that only the lasting tool-call log reads. They are optional and omitted when null, so
/// adding them is not a protocol change. This type deliberately does not reuse the names
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
/// <param name="Arguments">A compact summary of the call's input (its raw input JSON), or <see langword="null"/> on an update that did not carry input. Omitted from the wire when null.</param>
/// <param name="Result">The text one of Huddle's own App Tools returned, such as a refusal, or <see langword="null"/> for every other tool and for an update that carried none. Omitted from the wire when null.</param>
public sealed record ToolActivity(
    string RoomId,
    string MessageId,
    string ToolCallId,
    string? Title,
    ToolActivityStatus Status,
    string? Path = null,
    int? Line = null,
    EditChange? Edit = null,
    string? Arguments = null,
    string? Result = null) : ProtocolMessage;