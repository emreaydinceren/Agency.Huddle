namespace Agency.Huddle.App.Jobs;

/// <summary>
/// One <c>##</c> section of a <c>schedule.md</c>: a Job, or a section that could not be read, kept as written so a rewrite
/// by Huddle never loses what the Human typed.
/// </summary>
/// <param name="Job">The Job, or <see langword="null"/> when the section could not be read.</param>
/// <param name="Raw">The section as written, heading included; the text a rewrite keeps when <paramref name="Job"/> is null.</param>
/// <param name="Title">The section's heading.</param>
/// <param name="Problem">Why the section could not be read, or <see langword="null"/>.</param>
internal sealed record JobSection(Job? Job, string Raw, string Title, string? Problem);
