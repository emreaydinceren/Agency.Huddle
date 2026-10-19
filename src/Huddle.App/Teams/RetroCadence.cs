namespace Agency.Huddle.App.Teams;

/// <summary>How often a retrospective runs.</summary>
/// <param name="Every">The count of <paramref name="Unit"/> between retrospectives; always positive.</param>
/// <param name="Unit">What <paramref name="Every"/> counts.</param>
internal sealed record RetroCadence(int Every, CadenceUnit Unit);
