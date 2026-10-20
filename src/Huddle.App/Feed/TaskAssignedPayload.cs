namespace Agency.Huddle.App.Feed;

/// <summary>A Task was assigned to the Human.</summary>
/// <param name="Id">The Task id.</param>
/// <param name="Title">The Task title.</param>
/// <param name="Team">The Task's Team.</param>
/// <param name="PreviousAssignee">Who held it before, or <see langword="null"/>.</param>
internal sealed record TaskAssignedPayload(string Id, string Title, string Team, string? PreviousAssignee) : FeedPayload;
