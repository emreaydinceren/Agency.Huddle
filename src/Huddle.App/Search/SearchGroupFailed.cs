namespace Agency.Huddle.App.Search;

/// <summary>A provider threw; only its own group shows the failure (spec §7.3, D-20).</summary>
public sealed record SearchGroupFailed : SearchGroupState;
