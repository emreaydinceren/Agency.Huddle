namespace Agency.Huddle.App.Feed;

/// <summary>A gate refused an Agent's move and the Human must decide.</summary>
/// <param name="Id">The Task id.</param>
/// <param name="Title">The Task title.</param>
/// <param name="Team">The Task's Team.</param>
/// <param name="GateId">The gate's id.</param>
/// <param name="Message">The gate's message.</param>
/// <param name="FromStatus">The status the move left.</param>
/// <param name="ToStatus">The status the move aimed at.</param>
internal sealed record GateDecisionPayload(string Id, string Title, string Team, string GateId, string Message, string FromStatus, string ToStatus) : FeedPayload;
