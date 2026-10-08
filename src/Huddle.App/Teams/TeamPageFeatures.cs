namespace Agency.Huddle.App.Teams;

/// <summary>Which optional features the installation has on; decides which tabs exist.</summary>
/// <param name="Library">Whether the Library is on.</param>
/// <param name="Tasks">Whether Tasks are on.</param>
/// <param name="Process">Whether the Team has a <c>Process.md</c>, which adds the Process tab to a Team page.</param>
public sealed record TeamPageFeatures(bool Library, bool Tasks, bool Process = false);
