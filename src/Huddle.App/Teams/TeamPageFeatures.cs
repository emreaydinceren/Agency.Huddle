namespace Agency.Huddle.App.Teams;

/// <summary>Which optional features the installation has on; decides which tabs exist.</summary>
public sealed record TeamPageFeatures(bool Library, bool Tasks);
