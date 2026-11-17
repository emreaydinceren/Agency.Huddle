namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>Finds the newest pass in the Memory backups root, from the pass folders' journals alone.</summary>
/// <param name="backups">Names the Memory backups root.</param>
internal sealed class MemoryTidyLastPassReader(MemoryTidyBackups backups)
{
    /// <summary>Reads every pass folder's journal and picks the one that started last.</summary>
    /// <returns>The newest pass, or <see langword="null"/> when there is none.</returns>
    public MemoryTidyLastPass? Find()
    {
        string root = backups.RootPath;
        if (!Directory.Exists(root))
        {
            return null;
        }

        List<(DateTimeOffset At, string Folder, MemoryTidyCounts Counts)> passes = [];
        foreach (string folder in Directory.EnumerateDirectories(root))
        {
            IReadOnlyList<MemoryTidyEntry> entries = MemoryTidyJournal.Read(folder);
            if (entries.OfType<MemoryTidyEntry.PassStarted>().FirstOrDefault() is { } started)
            {
                passes.Add((started.At, folder, MemoryTidyList.Count(entries)));
            }
        }

        if (passes.Count == 0)
        {
            return null;
        }

        passes.Sort(static (left, right) => right.At.CompareTo(left.At));
        string? list = passes.Select(static pass => MemoryTidyList.PathIn(pass.Folder)).FirstOrDefault(File.Exists);
        return new MemoryTidyLastPass(passes[0].At, passes[0].Counts, list);
    }
}
