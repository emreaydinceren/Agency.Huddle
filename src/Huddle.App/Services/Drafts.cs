using System.Text;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Services;

/// <summary>
/// Holds every Turn's Draft while it is being written, keyed by <see cref="Draft.MessageId"/> rather
/// than by Room: a Group Room where two Agents were both Mentioned can have two Turns streaming into
/// it at once, and a Room-keyed store would silently drop one of them. Nothing here ever reaches the
/// Transcript — a Draft is, by definition, in memory only (<c>docs/engineering/language.md</c>) — and
/// each entry is removed the moment its Turn finishes; see <see cref="Complete"/>.
/// </summary>
/// <remarks>
/// Functional core, imperative shell: mutation happens only behind <see cref="gate"/>, onto a private
/// <see cref="StringBuilder"/>-backed state, and every method that hands text back out returns an
/// immutable <see cref="Draft"/> snapshot rather than a reference into that state.
/// </remarks>
internal sealed class Drafts
{
    /// <summary>
    /// The most text a single Draft will accumulate, in UTF-16 characters. This is a Singleton
    /// holding model output for the life of the process. A looping model, or an Agent killed without
    /// a clean disconnect (so no terminator ever arrives to call <see cref="Complete"/>), would
    /// otherwise grow a <see cref="StringBuilder"/> without bound inside a process-lifetime object.
    /// 256 KB is generous — a long reply is a few KB — and the real Message still arrives intact
    /// through <c>ChatService.PostAsync</c>, which is bounded separately by
    /// <c>JsonLineStream.MaxLineBytes</c> (1 MB) on the wire. Past the cap, <see cref="Append"/> stops
    /// growing the text and leaves the Draft as it stands, rather than truncating what is already
    /// there or discarding the Draft outright.
    /// </summary>
    internal const int MaxDraftTextLength = 256 * 1024;

    /// <summary>
    /// The most tool calls a Draft keeps. The Room view shows a short list, not a log: past this the
    /// oldest row is dropped, which also bounds what a Singleton holds per Turn (see the Turn detail
    /// spec, section 11).
    /// </summary>
    internal const int MaxToolCalls = 6;

    private readonly Lock gate = new();
    private readonly Dictionary<string, DraftState> drafts = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates the Draft for <paramref name="messageId"/> on first call and appends to it thereafter.
    /// Safe to call whether or not <see cref="Activity"/> already recorded a tool call for the same
    /// <paramref name="messageId"/> — whichever method is called first creates the Draft, and the
    /// other fills in what it did not set.
    /// </summary>
    /// <param name="messageId">The id the Message will have once this Turn is posted.</param>
    /// <param name="roomId">The Room the Turn is happening in.</param>
    /// <param name="agentId">The Agent writing this Turn.</param>
    /// <param name="senderName">The Agent's display name.</param>
    /// <param name="text">The increment of text that just arrived.</param>
    public void Append(string messageId, string roomId, string agentId, string senderName, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(senderName);
        ArgumentNullException.ThrowIfNull(text);

        lock (this.gate)
        {
            var state = this.GetOrCreate(messageId, roomId, agentId, senderName);
            var remaining = Drafts.MaxDraftTextLength - state.Text.Length;
            if (remaining <= 0)
            {
                return;
            }

            state.Text.Append(text.Length <= remaining ? text : text[..remaining]);
        }
    }

    /// <summary>
    /// Records one tool call the Agent is making. Calls are kept, oldest first, up to
    /// <see cref="MaxToolCalls"/>: an update with a known <paramref name="toolCallId"/> merges into its
    /// row, a new id appends one and, past the bound, drops the oldest. A null
    /// <paramref name="title"/>, <paramref name="path"/>, <paramref name="line"/> or
    /// <paramref name="edit"/> leaves the stored value alone, because ACP reads an omitted field as
    /// unchanged; <paramref name="status"/> always replaces. Safe to call before any
    /// <see cref="Append"/> for the same <paramref name="messageId"/>: a Draft may open with a tool
    /// call before it has any text.
    /// </summary>
    /// <param name="messageId">The id the Message will have once this Turn is posted.</param>
    /// <param name="toolCallId">The tool call this activity is about; the key its row is merged by.</param>
    /// <param name="roomId">The Room the Turn is happening in.</param>
    /// <param name="agentId">The Agent writing this Turn.</param>
    /// <param name="senderName">The Agent's display name.</param>
    /// <param name="title">What the Agent is doing, or <see langword="null"/> if this update did not say.</param>
    /// <param name="status">The lifecycle state of this tool call.</param>
    /// <param name="path">The file the call touches, or <see langword="null"/> if this update did not say.</param>
    /// <param name="line">The 1-based line in <paramref name="path"/>, or <see langword="null"/> if this update did not say.</param>
    /// <param name="edit">What the call changes, or <see langword="null"/> if this update did not say.</param>
    public void Activity(
        string messageId,
        string toolCallId,
        string roomId,
        string agentId,
        string senderName,
        string? title,
        ToolActivityStatus status,
        string? path = null,
        int? line = null,
        EditChange? edit = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolCallId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(senderName);

        lock (this.gate)
        {
            var state = this.GetOrCreate(messageId, roomId, agentId, senderName);
            int index = state.ToolCalls.FindIndex(call => string.Equals(call.ToolCallId, toolCallId, StringComparison.Ordinal));
            if (index < 0)
            {
                state.ToolCalls.Add(new ToolCallDetail(toolCallId, title, status, path, line, edit));
                if (state.ToolCalls.Count > Drafts.MaxToolCalls)
                {
                    state.ToolCalls.RemoveAt(0);
                }

                return;
            }

            ToolCallDetail existing = state.ToolCalls[index];
            state.ToolCalls[index] = new ToolCallDetail(
                toolCallId,
                title ?? existing.Title,
                status,
                path ?? existing.Path,
                line ?? existing.Line,
                edit ?? existing.Edit);
        }
    }

    /// <summary>
    /// Removes the Draft for <paramref name="messageId"/>, if there is one. Both the Turn's own
    /// terminator and a successful post through <c>ChatService.PostAsync</c> call this, so it must be
    /// — and is — harmless to call for a <paramref name="messageId"/> that has no Draft.
    /// </summary>
    /// <param name="messageId">The Turn whose Draft is finished.</param>
    public void Complete(string messageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        lock (this.gate)
        {
            this.drafts.Remove(messageId);
        }
    }

    /// <summary>An immutable snapshot of every Draft currently open in <paramref name="roomId"/>.</summary>
    /// <param name="roomId">The Room to report on.</param>
    /// <returns>
    /// The Room's open Drafts, in no particular order. A later mutation of any Draft through
    /// <see cref="Append"/> or <see cref="Activity"/> never changes a list already returned here.
    /// </returns>
    public IReadOnlyList<Draft> ForRoom(string roomId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        lock (this.gate)
        {
            var result = new List<Draft>();
            foreach (var (messageId, state) in this.drafts)
            {
                if (string.Equals(state.RoomId, roomId, StringComparison.Ordinal))
                {
                    result.Add(Drafts.ToDraft(messageId, state));
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Removes every open Draft belonging to <paramref name="agentId"/> — called when an Agent's
    /// session ends without ever sending a terminator for one or more of its Turns.
    /// </summary>
    /// <param name="agentId">The Agent whose Drafts should be dropped.</param>
    /// <returns>
    /// The distinct Room ids that held one of the removed Drafts, so the caller knows which Rooms to
    /// publish <see cref="RoomEvents.DraftChanged"/> for.
    /// </returns>
    public IReadOnlyList<string> ClearForAgent(string agentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        lock (this.gate)
        {
            var affectedRooms = new HashSet<string>(StringComparer.Ordinal);
            var toRemove = new List<string>();
            foreach (var (messageId, state) in this.drafts)
            {
                if (string.Equals(state.AgentId, agentId, StringComparison.Ordinal))
                {
                    toRemove.Add(messageId);
                    affectedRooms.Add(state.RoomId);
                }
            }

            foreach (var messageId in toRemove)
            {
                this.drafts.Remove(messageId);
            }

            return [.. affectedRooms];
        }
    }

    private static Draft ToDraft(string messageId, DraftState state)
    {
        return new Draft(
            messageId,
            state.RoomId,
            state.AgentId,
            state.SenderName,
            state.Text.ToString(),
            [.. state.ToolCalls]);
    }

    private DraftState GetOrCreate(string messageId, string roomId, string agentId, string senderName)
    {
        if (this.drafts.TryGetValue(messageId, out var existing))
        {
            return existing;
        }

        var created = new DraftState(roomId, agentId, senderName);
        this.drafts[messageId] = created;
        return created;
    }

    /// <summary>
    /// The mutable state one Draft accumulates behind <see cref="Drafts.gate"/>. Never leaves
    /// <see cref="Drafts"/> — every reader is handed an immutable <see cref="Draft"/> built from a
    /// snapshot of this instead.
    /// </summary>
    private sealed class DraftState
    {
        public DraftState(string roomId, string agentId, string senderName)
        {
            this.RoomId = roomId;
            this.AgentId = agentId;
            this.SenderName = senderName;
        }

        public string RoomId { get; }

        public string AgentId { get; }

        public string SenderName { get; }

        public StringBuilder Text { get; } = new();

        public List<ToolCallDetail> ToolCalls { get; } = [];
    }
}
