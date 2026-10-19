namespace Agency.Huddle.App.Teams;

/// <summary>An adapter that answered its model probe, with the model ids it lists.</summary>
/// <param name="AdapterId">The adapter id.</param>
/// <param name="Models">The model ids the adapter advertises.</param>
internal sealed record AdapterAvailability(string AdapterId, IReadOnlyList<string> Models);
