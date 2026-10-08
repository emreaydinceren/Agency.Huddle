using System.Text;

namespace Agency.Huddle.App.Skills;

/// <summary>The kind of thing a review flag points at.</summary>
internal enum ReviewFlagKind
{
    /// <summary>A script or executable file.</summary>
    Script,

    /// <summary>A shell command in the skill text.</summary>
    ShellCommand,

    /// <summary>A network call.</summary>
    NetworkCall,

    /// <summary>A path outside the Work Dir.</summary>
    PathOutsideWorkDir,

    /// <summary>No license declared.</summary>
    NoLicense
}

/// <summary>One finding for the user to review before install.</summary>
/// <param name="Kind">What was found.</param>
/// <param name="File">Path relative to the skill folder, or <c>.</c> for the skill as a whole.</param>
/// <param name="Line">One-based line number.</param>
/// <param name="Excerpt">The offending text, at most 120 characters.</param>
internal sealed record ReviewFlag(ReviewFlagKind Kind, string File, int Line, string Excerpt);

/// <summary>
/// Scans a downloaded skill folder for things a user should look at before installing it: scripts,
/// shell commands, network calls, paths outside the Work Dir and a missing license (SK-6, NF-4, D5).
/// It only warns; it never blocks.
/// </summary>
internal static class SkillReviewScanner
{
    private const int MaxExcerpt = 120;

    private static readonly string[] ScriptExtensions =
        [".ps1", ".sh", ".bash", ".bat", ".cmd", ".py", ".js", ".ts", ".mjs", ".exe", ".dll"];

    private static readonly string[] CommandNames =
        ["curl", "wget", "Invoke-WebRequest", "Invoke-RestMethod", "iwr", "npm", "pip", "git clone", "rm -rf", "Remove-Item", "sudo"];

#pragma warning disable S1075 // The system roots are the data this scanner exists to match, not configuration.
    private static readonly string[] BoundedPathPrefixes = ["/etc", "/usr", "C:\\Windows"];
#pragma warning restore S1075

    private static readonly string[] OpenPathPrefixes = ["~/", "$HOME", "%USERPROFILE%", "..\\", "../"];

    private static readonly char[] TokenSeparators = [' ', '\t', '"', '\'', '`', '(', ')', '[', ']', '<', '>', ',', ';'];

    /// <summary>Scans the files of one skill folder.</summary>
    /// <param name="files">File contents keyed by path relative to the skill folder.</param>
    /// <param name="license">The declared license; <c>none</c> or empty means no license.</param>
    /// <returns>The flags ordered by kind, then file, then line.</returns>
    public static IReadOnlyList<ReviewFlag> Scan(IReadOnlyDictionary<string, byte[]> files, string license)
    {
        ArgumentNullException.ThrowIfNull(files);

        List<ReviewFlag> flags = [];
        foreach ((string path, byte[] content) in files)
        {
            if (IsScript(path))
            {
                flags.Add(new ReviewFlag(ReviewFlagKind.Script, path, 1, Trim(path[(path.LastIndexOf('/') + 1)..])));
                continue;
            }

            ScanText(path, Encoding.UTF8.GetString(content), flags);
        }

        if (string.IsNullOrWhiteSpace(license) || string.Equals(license.Trim(), "none", StringComparison.OrdinalIgnoreCase))
        {
            flags.Add(new ReviewFlag(ReviewFlagKind.NoLicense, ".", 1, "No license declared"));
        }

        return flags
            .OrderBy(static f => f.Kind)
            .ThenBy(static f => f.File, StringComparer.Ordinal)
            .ThenBy(static f => f.Line)
            .ToList();
    }

    private static bool IsScript(string path)
    {
        foreach (string extension in ScriptExtensions)
        {
            if (path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void ScanText(string path, string text, List<ReviewFlag> flags)
    {
        bool inFence = false;
        string[] lines = text.Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index].TrimEnd('\r');
            string trimmed = line.Trim();
            int number = index + 1;
            if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                inFence = !inFence;
                continue;
            }

            bool indented = line.Length > 0 && (line[0] == ' ' || line[0] == '\t');
            bool isCommand = (inFence || indented) && IsCommand(trimmed);
            if (isCommand)
            {
                flags.Add(new ReviewFlag(ReviewFlagKind.ShellCommand, path, number, Trim(trimmed)));
            }

            if ((inFence || isCommand) && HasUrl(trimmed))
            {
                flags.Add(new ReviewFlag(ReviewFlagKind.NetworkCall, path, number, Trim(trimmed)));
            }

            if (HasOutsidePath(trimmed))
            {
                flags.Add(new ReviewFlag(ReviewFlagKind.PathOutsideWorkDir, path, number, Trim(trimmed)));
            }
        }
    }

    private static bool IsCommand(string trimmed)
    {
        string candidate = trimmed;
        if (candidate.StartsWith("$ ", StringComparison.Ordinal) || candidate.StartsWith("> ", StringComparison.Ordinal))
        {
            candidate = candidate[2..].TrimStart();
        }

        foreach (string name in CommandNames)
        {
            if (candidate.StartsWith(name, StringComparison.OrdinalIgnoreCase)
                && (candidate.Length == name.Length || char.IsWhiteSpace(candidate[name.Length])))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasUrl(string trimmed) =>
        trimmed.Contains("http://", StringComparison.OrdinalIgnoreCase)
        || trimmed.Contains("https://", StringComparison.OrdinalIgnoreCase);

    private static bool HasOutsidePath(string trimmed)
    {
        foreach (string token in trimmed.Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (string prefix in OpenPathPrefixes)
            {
                if (token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            foreach (string prefix in BoundedPathPrefixes)
            {
                if (token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    && (token.Length == prefix.Length || token[prefix.Length] is '/' or '\\'))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string Trim(string value) => value.Length <= MaxExcerpt ? value : value[..MaxExcerpt];
}
