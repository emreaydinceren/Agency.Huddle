using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Tasks;

/// <summary>A move an Agent attempted that a process gate refused and only the Human can override.</summary>
/// <param name="Current">The Task as it is.</param>
/// <param name="Requested">The status the Agent asked for.</param>
/// <param name="Actor">The Agent that asked.</param>
/// <param name="Gate">The gate that refused.</param>
/// <param name="Message">The refusal the Agent was shown.</param>
internal sealed record TaskMoveRefusal(TaskItem Current, TaskState Requested, TaskActor Actor, GateId Gate, string Message);
