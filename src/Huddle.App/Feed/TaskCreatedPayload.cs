namespace Agency.Huddle.App.Feed;

/// <summary>A Task was created.</summary>
/// <param name="Id">The Task id.</param>
/// <param name="Title">The Task title.</param>
/// <param name="Team">The Task's Team.</param>
/// <param name="Assignee">Who it was assigned to, or <see langword="null"/>.</param>
internal sealed record TaskCreatedPayload(string Id, string Title, string Team, string? Assignee) : FeedPayload;
