namespace Agency.Huddle.App.HiringPool;

/// <summary>A pool entry that could not be offered.</summary>
/// <param name="Id">The job or team type id, or the file name when the whole file was rejected.</param>
/// <param name="Source">Where the entry came from.</param>
/// <param name="Message">Why it was rejected.</param>
internal sealed record HiringPoolProblem(string Id, string Source, string Message);
