using Agency.Huddle.App.Data;

namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>
/// The day rules of Memory Tidy spec §6.5 that follow a write: a file keeps its day through a describe or a rewrite, a merge takes the
/// latest day of its sources, and a removed file loses its row. The applier calls these after each step, for one folder.
/// </summary>
/// <param name="store">Holds the file rows.</param>
/// <param name="folderKey">The folder the rows belong to.</param>
internal sealed class MemoryTidyDays(MemoryTidyStateStore store, string folderKey)
{
    /// <summary>A file was rewritten or described: its row gets the new version and keeps its day.</summary>
    /// <param name="file">The file's name.</param>
    /// <param name="newVersion">The version the write made.</param>
    public void Kept(string file, string newVersion)
    {
        if (this.Find(file) is { } row)
        {
            store.PutFile(row with { Version = newVersion });
        }
    }

    /// <summary>Files were merged into one: its row gets the new version and the latest day among the sources, so old content never looks newer.</summary>
    /// <param name="into">The file the merge wrote.</param>
    /// <param name="sources">The files merged, <paramref name="into"/> among them when it was one.</param>
    /// <param name="newVersion">The version the write made.</param>
    public void Merged(string into, IReadOnlyList<string> sources, string newVersion)
    {
        ArgumentNullException.ThrowIfNull(sources);

        DateOnly? latest = null;
        foreach (string source in sources)
        {
            if (this.Find(source) is { } row && (latest is null || row.WrittenDay > latest))
            {
                latest = row.WrittenDay;
            }
        }

        if (latest is { } day)
        {
            store.PutFile(new MemoryTidyFileRow(folderKey, into, newVersion, day));
        }
    }

    /// <summary>A file was removed by the pass: its row goes.</summary>
    /// <param name="file">The file's name.</param>
    public void Removed(string file)
    {
        store.RemoveFile(folderKey, file);
    }

    private MemoryTidyFileRow? Find(string file) =>
        store.GetFiles(folderKey).FirstOrDefault(row => string.Equals(row.File, file, StringComparison.Ordinal));
}
