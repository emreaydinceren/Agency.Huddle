using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.App.SkillSources;

/// <summary>One folder a watcher covers.</summary>
/// <param name="Path">The folder.</param>
/// <param name="Recursive">Whether sub-folders count.</param>
internal sealed record WatchRoot(string Path, bool Recursive);

/// <summary>What to watch and what to poll.</summary>
/// <param name="Watched">At most <see cref="WatchRoots.MaxWatchers"/> roots with a <see cref="FileSystemWatcher"/>.</param>
/// <param name="Polled">Roots polled every 30 seconds: missing paths, network paths and roots past the cap.</param>
internal sealed record WatchPlan(IReadOnlyList<WatchRoot> Watched, IReadOnlyList<WatchRoot> Polled);

/// <summary>The watch-root rule of §8.5, as a pure function.</summary>
internal static class WatchRoots
{
    /// <summary>The most file watchers created in total.</summary>
    public const int MaxWatchers = 32;

    /// <summary>Plans the watchers for a registry: the Skill Sources folder (recursive); each folder outside it that holds a
    /// pack or catalog file (not recursive); and each Skill folder outside it by its parent, nested roots collapsed
    /// (recursive). A root that does not exist or is on a network drive is polled, as is every root past the cap.</summary>
    public static WatchPlan Plan(string skillSourcesDir, ResolvedRegistry registry, Func<string, bool> exists, Func<string, bool> isNetwork)
    {
        ArgumentNullException.ThrowIfNull(skillSourcesDir);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(exists);
        ArgumentNullException.ThrowIfNull(isNetwork);
        List<WatchRoot> roots = [new WatchRoot(skillSourcesDir, true)];

        IEnumerable<string?> files = registry.Packs.Select(static pack => pack.FilePath).Concat(registry.Catalogs.Select(static catalog => catalog.FilePath));
        foreach (string folder in files.OfType<string>()
            .Where(file => !Checks.IsUnder(file, skillSourcesDir))
            .Select(static file => Path.GetDirectoryName(file) ?? file)
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            roots.Add(new WatchRoot(folder, false));
        }

        List<string> skillParents = [.. registry.Skills
            .Select(static skill => skill.FolderPath)
            .OfType<string>()
            .Where(folder => !Checks.IsUnder(folder, skillSourcesDir))
            .Select(static folder => Path.GetDirectoryName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? folder)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
        foreach (string parent in Collapse(skillParents))
        {
            roots.Add(new WatchRoot(parent, true));
        }

        roots = [.. roots.Where(root => root.Recursive || !roots.Any(other => other.Recursive && Checks.IsUnder(root.Path, other.Path)))];
        List<WatchRoot> watched = [];
        List<WatchRoot> polled = [];
        foreach (WatchRoot root in roots)
        {
            if (watched.Count < MaxWatchers && exists(root.Path) && !isNetwork(root.Path))
            {
                watched.Add(root);
            }
            else
            {
                polled.Add(root);
            }
        }

        return new WatchPlan(watched, polled);
    }

    /// <summary>Drops every folder that lies under another one in the list.</summary>
    public static IReadOnlyList<string> Collapse(IReadOnlyList<string> folders)
    {
        ArgumentNullException.ThrowIfNull(folders);
        return [.. folders.Where(folder => !folders.Any(other => !string.Equals(other, folder, StringComparison.OrdinalIgnoreCase) && Checks.IsUnder(folder, other)))];
    }
}
