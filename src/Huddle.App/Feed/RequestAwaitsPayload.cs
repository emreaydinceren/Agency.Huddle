namespace Agency.Huddle.App.Feed;

/// <summary>A Request awaits the Human.</summary>
/// <param name="Id">The Task id.</param>
/// <param name="Title">The Task title.</param>
/// <param name="Team">The Team that holds the Request.</param>
/// <param name="FromTeam">The Team that asked.</param>
/// <param name="Kind">The kind of Request.</param>
internal sealed record RequestAwaitsPayload(string Id, string Title, string Team, string FromTeam, string Kind) : FeedPayload;
