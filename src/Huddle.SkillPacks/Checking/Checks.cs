using Agency.Huddle.SkillPacks.Reading;
using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.SkillPacks.Checking;

/// <summary>Runs every check once (§6.3). Huddle and <c>huddle-packs</c> call the same method, so their findings cannot
/// drift. Reads files and writes nothing.</summary>
public static class Checks
{
    /// <summary>The longest <c>consultWhenAdds</c> (P007).</summary>
    public const int MaxConsultWhen = 200;

    /// <summary>The largest NOTES.md (O004).</summary>
    public const int MaxNotesBytes = 16 * 1024;

    /// <summary>Runs every check once and returns the findings, row results, states and summary.</summary>
    /// <param name="registry">What the resolver produced.</param>
    /// <param name="context">What this Huddle knows about itself.</param>
    /// <param name="options">Strict, the path filter and the caller's version.</param>
    /// <param name="files">The same reader the resolver used.</param>
    /// <param name="cancellationToken">Cancels the run; no partial result is returned.</param>
    public static CheckResult Run(
        ResolvedRegistry registry,
        CheckContext context,
        CheckOptions options,
        IFileReader files,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(files);
        return new CheckRun(registry, context, options, files, cancellationToken).Execute();
    }

    /// <summary>Runs the Skill checks on one folder that no row reaches (<c>huddle-packs validate &lt;folder&gt;</c>): a
    /// virtual Skill, so the result has no rows and one Skill.</summary>
    public static CheckResult RunSkillFolder(
        string folder,
        CheckContext context,
        CheckOptions options,
        IFileReader files,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folder);
        return RunSkillFolder(folder, Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), context, options, files, cancellationToken);
    }

    /// <summary>Runs the Skill checks on one folder under the name <paramref name="skillName"/>: the copier checks a staged
    /// copy, whose folder is named <c>&lt;name&gt;-&lt;random&gt;</c>, as the Skill it will become (§8.6 step 5).</summary>
    public static CheckResult RunSkillFolder(
        string folder,
        string skillName,
        CheckContext context,
        CheckOptions options,
        IFileReader files,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(skillName);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(files);
        return new CheckRun(ResolvedRegistry.Empty, context, options, files, cancellationToken).ExecuteFolder(folder, skillName);
    }

    /// <summary>Whether a row's resolved file is <paramref name="path"/> or under it, or the row reaches a Skill folder
    /// that is (the rows <c>validate &lt;path&gt;</c> checks, §6.4).</summary>
    public static bool RowReaches(ResolvedRegistry registry, RegistryRow row, string path)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(path);
        string? file = row.Kind == RowKind.Pack ? registry.Packs[row.Index].FilePath : registry.Catalogs[row.Index].FilePath;
        if (file is not null && IsUnder(file, path))
        {
            return true;
        }

        return registry.Skills.Any(skill => skill.FolderPath is not null
            && IsUnder(skill.FolderPath, path)
            && skill.Claims.Any(claim => claim.Kind == row.Kind && claim.RowIndex == row.Index));
    }

    /// <summary>Whether <paramref name="path"/> is <paramref name="root"/> or lies under it, ignoring case.</summary>
    public static bool IsUnder(string path, string root)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(root);
        string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string top = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(full, top, StringComparison.OrdinalIgnoreCase)
            || full.StartsWith(top + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
