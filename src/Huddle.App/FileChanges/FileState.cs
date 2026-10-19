namespace Agency.Huddle.App.FileChanges;

/// <summary>What one Agent last saw, per Room. Saved as <c>{DataDir}/file-state/&lt;Name&gt;.json</c>.</summary>
/// <param name="Subscribed">Entries added by <c>watch_folder</c>, in call order.</param>
/// <param name="Rooms">This Agent's last-shown baseline, keyed by Room id.</param>
/// <param name="Writers">This Agent's own last writes, keyed by entry then by relative path.</param>
internal sealed record FileState(
    IReadOnlyList<string> Subscribed,
    IReadOnlyDictionary<string, RoomBaseline> Rooms,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, FileWriter>> Writers)
{
    /// <summary>An empty <see cref="FileState"/>, with every dictionary keyed per FC §6.4's key comparison rules.</summary>
    internal static FileState Empty { get; } = new(
        [],
        new Dictionary<string, RoomBaseline>(StringComparer.Ordinal),
        new Dictionary<string, IReadOnlyDictionary<string, FileWriter>>(StringComparer.OrdinalIgnoreCase));
}
