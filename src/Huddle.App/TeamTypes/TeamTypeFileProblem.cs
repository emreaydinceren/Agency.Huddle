namespace Agency.Huddle.App.TeamTypes;

/// <summary>A team type that could not be offered.</summary>
/// <param name="TypeId">The type id, taken from its folder name.</param>
/// <param name="Path">Where the manifest came from.</param>
/// <param name="Message">Why it was rejected.</param>
internal sealed record TeamTypeFileProblem(string TypeId, string Path, string Message);
