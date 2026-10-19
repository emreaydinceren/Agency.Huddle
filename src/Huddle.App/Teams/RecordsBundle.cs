using Agency.Huddle.App.Data;

namespace Agency.Huddle.App.Teams;

/// <summary>The compact evidence a retro starts from (RT-3, RT-8, RT-9).</summary>
/// <param name="Team">The Team's display name.</param>
/// <param name="Since">The cutoff; nothing older is included.</param>
/// <param name="Tasks">Tasks of the Team with a change since the cutoff.</param>
/// <param name="ReviewBounces">Review to In Progress or To Do changes.</param>
/// <param name="BudgetPauses">Change log entries that mention a spent budget.</param>
/// <param name="WakeLimitsHit">Change log entries that mention a wake limit.</param>
/// <param name="Escalations">Declines and escalations.</param>
/// <param name="SpendByMember">Each member's stored spend.</param>
/// <param name="Requests">Requests with a change since the cutoff.</param>
/// <param name="ToolCalls">The tool-call log, aggregated.</param>
internal sealed record RecordsBundle(
    string Team,
    DateTimeOffset Since,
    IReadOnlyList<TaskRecord> Tasks,
    IReadOnlyList<RecordEvent> ReviewBounces,
    IReadOnlyList<RecordEvent> BudgetPauses,
    IReadOnlyList<RecordEvent> WakeLimitsHit,
    IReadOnlyList<RecordEvent> Escalations,
    IReadOnlyDictionary<string, IReadOnlyList<StoredSpend>> SpendByMember,
    IReadOnlyList<RequestRecord> Requests,
    IReadOnlyList<ToolCallSummary> ToolCalls)
{
    /// <summary>How many tool-call log lines or files could not be read and were skipped.</summary>
    public int Unreadable { get; init; }
}
