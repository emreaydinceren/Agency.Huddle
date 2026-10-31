namespace Agency.Huddle.App.Data;

/// <summary>What the cadence service last did for one Team.</summary>
/// <param name="Team">The Team's name, compared ignoring case.</param>
/// <param name="LastDigestUtc">When the digest last fired, or the baseline set on first sight; <see langword="null"/> before either.</param>
/// <param name="LastRetroUtc">When the retro last fired, or the baseline set on first sight; <see langword="null"/> before either.</param>
/// <param name="DoneAtLastRetro">The number of Done Tasks at <paramref name="LastRetroUtc"/>; <see langword="null"/> before the baseline.</param>
public sealed record CadenceState(string Team, DateTimeOffset? LastDigestUtc, DateTimeOffset? LastRetroUtc, int? DoneAtLastRetro);
