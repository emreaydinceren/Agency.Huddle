using System.Buffers;
using System.Globalization;

namespace Agency.Huddle.SkillPacks.Folders;

/// <summary>The limit a Skill folder broke: its size (S003) or a name or link Windows cannot keep (S004).</summary>
public enum FolderLimit
{
    /// <summary>More than 200 files or 5 MB in total (S003).</summary>
    Size,

    /// <summary>A link, a name Windows cannot store, or two names that differ only in case (S004).</summary>
    Name,
}

/// <summary>One broken folder rule.</summary>
/// <param name="Limit">Which limit.</param>
/// <param name="Path">The full path of the file or folder at fault, or the Skill folder for a size rule.</param>
/// <param name="Message">One line naming the file and the rule.</param>
public sealed record FolderViolation(FolderLimit Limit, string Path, string Message);

/// <summary>
/// The rules the deleted <c>SkillDownloader</c> held for a Skill folder (§6.3): at most 200 files and 5 MB, no symbolic
/// link or junction, no name Windows cannot store, no two names that differ only in case.
/// </summary>
public static class SkillFolderRules
{
    /// <summary>The most files a Skill folder may hold.</summary>
    public const int MaxFiles = 200;

    /// <summary>The most bytes a Skill folder may hold.</summary>
    public const long MaxTotalBytes = 5L * 1024 * 1024;

    private static readonly SearchValues<char> ForbiddenWindowsChars = SearchValues.Create("<>\"|?*:");

    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>Checks <paramref name="folder"/> through <paramref name="files"/>; a <c>.git</c> folder is skipped.</summary>
    /// <returns>Every broken rule, size rules first, then names in path order.</returns>
    public static IReadOnlyList<FolderViolation> Check(IFileReader files, string folder)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(folder);
        List<FolderViolation> names = [];
        List<string> all = [];
        Walk(files, folder, folder, all, names);

        List<FolderViolation> result = [];
        long total = 0;
        foreach (string file in all)
        {
            total += files.Length(file);
        }

        if (all.Count > MaxFiles)
        {
            result.Add(new FolderViolation(FolderLimit.Size, folder, string.Create(CultureInfo.InvariantCulture, $"{all.Count} files; the limit is {MaxFiles}.")));
        }

        if (total > MaxTotalBytes)
        {
            result.Add(new FolderViolation(FolderLimit.Size, folder, string.Create(CultureInfo.InvariantCulture, $"{total:N0} bytes; the limit is 5 MB.")));
        }

        result.AddRange(names);
        return result;
    }

    /// <summary>Whether a single path segment can be stored on Windows.</summary>
    public static bool IsWindowsStorable(string segment)
    {
        ArgumentNullException.ThrowIfNull(segment);
        if (segment.Length == 0
            || segment.AsSpan().IndexOfAny(ForbiddenWindowsChars) >= 0
            || segment.Any(char.IsControl)
            || segment.EndsWith('.')
            || segment.EndsWith(' '))
        {
            return false;
        }

        int dot = segment.IndexOf('.', StringComparison.Ordinal);
        string stem = dot < 0 ? segment : segment[..dot];
        return !ReservedDeviceNames.Contains(stem.TrimEnd(' '));
    }

    /// <summary>Walks one folder level, collecting files and name violations, never following a link.</summary>
    private static void Walk(IFileReader files, string root, string directory, List<string> all, List<FolderViolation> violations)
    {
        List<string> entries = [.. files.EnumerateFiles(directory, recursive: false), .. files.EnumerateDirectories(directory)];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (string entry in entries.Order(StringComparer.Ordinal))
        {
            string name = Path.GetFileName(entry);
            string relative = Path.GetRelativePath(root, entry).Replace('\\', '/');
            if (string.Equals(name, ".git", StringComparison.Ordinal) && files.DirectoryExists(entry))
            {
                continue;
            }

            if (!seen.Add(name))
            {
                violations.Add(new FolderViolation(FolderLimit.Name, entry, $"'{relative}' differs from another name only in case."));
            }

            if (!IsWindowsStorable(name))
            {
                violations.Add(new FolderViolation(FolderLimit.Name, entry, $"'{relative}' is a name Windows cannot store."));
            }

            if ((files.Attributes(entry) & FileAttributes.ReparsePoint) != 0)
            {
                violations.Add(new FolderViolation(FolderLimit.Name, entry, $"'{relative}' is a symbolic link or junction."));
                continue;
            }

            if (files.DirectoryExists(entry))
            {
                Walk(files, root, entry, all, violations);
            }
            else
            {
                all.Add(entry);
            }
        }
    }
}
