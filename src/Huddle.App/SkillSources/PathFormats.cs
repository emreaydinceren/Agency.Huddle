namespace Agency.Huddle.App.SkillSources;

/// <summary>Converts a full Windows path into each <see cref="PathFormat"/> (§6.8, "Copy path formats"). Pure.</summary>
internal static class PathFormats
{
    private const string ExtendedPrefix = @"\\?\";
    private const string WslDollar = @"\\wsl$\";
    private const string WslLocalhost = @"\\wsl.localhost\";

    /// <summary>The path in <paramref name="format"/>. A path that already starts with <c>/</c> is returned unchanged.</summary>
    /// <exception cref="ArgumentException">The path has no drive and no leading <c>/</c> or <c>\\</c>.</exception>
    public static string Convert(string path, PathFormat format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (path.StartsWith('/'))
        {
            return path;
        }

        string value = path.StartsWith(ExtendedPrefix, StringComparison.Ordinal) ? path[ExtendedPrefix.Length..] : path;
        bool unc = value.StartsWith(@"\\", StringComparison.Ordinal);
        bool drive = value.Length >= 2 && char.IsAsciiLetter(value[0]) && value[1] == ':';
        if (!unc && !drive)
        {
            throw new ArgumentException($"'{path}' is not a full path.", nameof(path));
        }

        return format switch
        {
            PathFormat.Windows => unc ? value : value.Replace('/', '\\'),
            PathFormat.ForwardSlashes => value.Replace('\\', '/'),
            _ => ToWsl(value, unc),
        };
    }

    /// <summary>The <c>\\wsl$\</c> or <c>\\wsl.localhost\</c> prefix a UNC path starts with, or null.</summary>
    private static string? WslPrefix(string value)
    {
        if (value.StartsWith(WslDollar, StringComparison.OrdinalIgnoreCase))
        {
            return WslDollar;
        }

        return value.StartsWith(WslLocalhost, StringComparison.OrdinalIgnoreCase) ? WslLocalhost : null;
    }

    /// <summary>The WSL form: <c>X:\rest</c> to <c>/mnt/x/rest</c>; a <c>\\wsl$\distro\</c> path to its Linux path; other UNC
    /// paths with forward slashes.</summary>
    private static string ToWsl(string value, bool unc)
    {
        if (unc)
        {
            string? prefix = WslPrefix(value);
            if (prefix is null)
            {
                return value.Replace('\\', '/');
            }

            string rest = value[prefix.Length..];
            int slash = rest.IndexOf('\\', StringComparison.Ordinal);
            return slash < 0 ? "/" : rest[slash..].Replace('\\', '/');
        }

        string tail = value.Length > 2 ? value[2..].Replace('\\', '/') : "/";
        return $"/mnt/{char.ToLowerInvariant(value[0])}{(tail.StartsWith('/') ? tail : "/" + tail)}";
    }
}
