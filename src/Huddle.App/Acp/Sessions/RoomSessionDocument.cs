namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>The JSON shape of one Agent's stored Room Sessions, per RS §6.6.</summary>
/// <param name="Rooms">Keyed by Room id, case preserved (correction item 19).</param>
internal sealed record RoomSessionDocument(Dictionary<string, RoomSessionEntry> Rooms);
