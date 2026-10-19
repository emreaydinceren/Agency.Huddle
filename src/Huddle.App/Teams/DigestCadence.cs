namespace Agency.Huddle.App.Teams;

/// <summary>How often a digest is written.</summary>
/// <param name="Every">The count of <paramref name="Unit"/> between digests; always positive.</param>
/// <param name="Unit">What <paramref name="Every"/> counts.</param>
internal sealed record DigestCadence(int Every, CadenceUnit Unit);
