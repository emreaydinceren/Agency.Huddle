namespace Agency.Huddle.App.NeedsYou;

/// <summary>One Room waiting on the Human, as the tracker reports it.</summary>
/// <param name="RoomId">The Room's id. Never its Name: Rooms are renamed.</param>
/// <param name="Reasons">Every reason the Room is waiting; never <see cref="NeedsYouReason.None"/>.</param>
/// <param name="Since">When the earliest of those reasons began.</param>
/// <param name="AskedBy">The Name of whoever began that earliest reason.</param>
internal sealed record NeedsYouEntry(string RoomId, NeedsYouReason Reasons, DateTimeOffset Since, string AskedBy);
