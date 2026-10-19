namespace Agency.Huddle.App.TeamTypes;

/// <summary>One reason a team type manifest is invalid.</summary>
/// <param name="Code">The stable rule code, such as <c>no-manager</c>.</param>
/// <param name="Message">The reason, naming the role or file concerned.</param>
internal sealed record TeamTypeProblem(string Code, string Message);
