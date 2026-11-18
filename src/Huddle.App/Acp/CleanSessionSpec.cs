namespace Agency.Huddle.App.Acp;

/// <summary>What a clean (goldfish) session is opened for. The host builds the read-deny rules at each open, so none is carried here.</summary>
/// <param name="PossessionId">The possession this session serves.</param>
/// <param name="RoomId">The Room the possession was granted in.</param>
/// <param name="TaskId">The Task the possession is about, or <see langword="null"/> when there is none.</param>
internal sealed record CleanSessionSpec(string PossessionId, string RoomId, string? TaskId);
