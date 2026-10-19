namespace Agency.Huddle.App.Teams;

/// <summary>The outcome of one hire.</summary>
/// <param name="Ok">Whether the Teammate file was written.</param>
/// <param name="Error">Why the hire was refused, or what failed after the Teammate was written; <see langword="null"/> when nothing went wrong.</param>
/// <param name="PersonaName">The new Teammate's name when the file was written; otherwise <see langword="null"/>.</param>
internal sealed record HireResult(bool Ok, string? Error, string? PersonaName);
