namespace Agency.Huddle.App.Jobs;

/// <summary>A section of a <c>schedule.md</c> that could not be read, shown in the Scheduled view so it is never silently ignored.</summary>
/// <param name="Owner">Whose file it is in.</param>
/// <param name="File">The file's path.</param>
/// <param name="Title">The section's heading.</param>
/// <param name="Reason">Why it could not be read.</param>
internal sealed record JobProblem(JobOwner Owner, string File, string Title, string Reason);
