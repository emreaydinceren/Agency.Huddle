using Agency.Huddle.App.Data;

namespace Agency.Huddle.App.Services;

/// <summary>A <c>waiting</c> row handed out for an Agent, with whether a Turn already started on it once.</summary>
/// <param name="Row">The row.</param>
/// <param name="CutOff">Whether a prompt was sent for it before (<c>attempts</c> of 1 or more): a Turn that a restart or a dropped connection cut off.</param>
internal sealed record WaitingTurn(TurnQueueRow Row, bool CutOff);
