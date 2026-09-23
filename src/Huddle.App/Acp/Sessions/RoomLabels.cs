namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>
/// D16 P0-3 (RS §8.1, RS §2 U15, RS D-20): distinguishes same-named Rooms in a Turn's label. A pure,
/// stateless function — <see cref="PersonaRunner"/> owns the mutable table of what it currently
/// knows each Room is called; this only decides, given that table, what one Room's label should say.
/// </summary>
internal static class RoomLabels
{
    /// <summary>How many trailing characters of a clashing Room's id are appended as its suffix.</summary>
    private const int SuffixLength = 6;

    /// <summary>
    /// Returns <paramref name="roomName"/> unchanged, unless some Room other than
    /// <paramref name="roomId"/> in <paramref name="knownNames"/> carries the same name,
    /// case-insensitively — a model reads "Nova" and "nova" as one name. When it does,
    /// <paramref name="roomName"/> is suffixed with <c>" #"</c> and the last
    /// <see cref="SuffixLength"/> characters of <paramref name="roomId"/> (the whole id, when it is
    /// shorter than that), so the label alone tells the two Rooms apart.
    /// </summary>
    /// <param name="roomId">The Room whose label is being built.</param>
    /// <param name="roomName">That Room's current name.</param>
    /// <param name="knownNames">
    /// Every Room id the runner currently knows of, mapped to that Room's current name, including
    /// <paramref name="roomId"/> itself — which is excluded from the clash check, since a Room is
    /// never a clash with itself.
    /// </param>
    /// <returns><paramref name="roomName"/>, suffixed only when another known Room shares it.</returns>
    /// <remarks>
    /// A Room the Agent has since left, or that was deleted, can linger in <paramref name="knownNames"/>
    /// with a stale name until the runner restarts (nothing on the pipe tells it a Room disappeared —
    /// RS §6.13's "Delete" row). Until then, its stale entry can still force a suffix onto a surviving
    /// Room that would otherwise be unique again; that is judged the safer failure mode, since it is
    /// never wrong for a model to see a suffix it did not strictly need, only for one it did need to
    /// be missing.
    /// </remarks>
    internal static string Distinguish(string roomId, string roomName, IReadOnlyDictionary<string, string> knownNames)
    {
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(roomName);
        ArgumentNullException.ThrowIfNull(knownNames);

        var sharedByAnotherRoom = knownNames.Any(entry =>
            !string.Equals(entry.Key, roomId, StringComparison.Ordinal) &&
            string.Equals(entry.Value, roomName, StringComparison.OrdinalIgnoreCase));

        if (!sharedByAnotherRoom)
        {
            return roomName;
        }

        var suffix = roomId.Length <= SuffixLength ? roomId : roomId[^SuffixLength..];
        return $"{roomName} #{suffix}";
    }
}
