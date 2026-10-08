using System.Globalization;
using System.IO.Compression;

namespace Agency.Huddle.App.Skills;

/// <summary>The files of one downloaded skill folder (SK-4).</summary>
/// <param name="Files">File bytes keyed by path relative to the skill folder, using <c>/</c> separators.</param>
internal sealed record DownloadedSkill(IReadOnlyDictionary<string, byte[]> Files);

/// <summary>The outcome of a download: the skill, or a plain error message.</summary>
/// <param name="Skill">The downloaded folder, or <see langword="null"/> when <paramref name="Error"/> is set.</param>
/// <param name="Error">What went wrong, or <see langword="null"/> on success.</param>
internal sealed record DownloadResult(DownloadedSkill? Skill, string? Error);

/// <summary>
/// Downloads one skill folder from a GitHub repository at a commit using the codeload zip archive, with no git
/// installed and nothing written to disk (SK-4, SK-10, NF-10).
/// </summary>
/// <param name="http">The client used for the archive request; it carries the request timeout.</param>
/// <param name="logger">The logger.</param>
/// <param name="maxDownloadBytes">The largest archive that is read into memory; a bigger one is refused.</param>
internal sealed class SkillDownloader(HttpClient http, ILogger<SkillDownloader> logger, long maxDownloadBytes = SkillDownloader.DefaultMaxDownloadBytes)
{
    /// <summary>The default limit on the size of the downloaded archive: 50 MB.</summary>
    public const long DefaultMaxDownloadBytes = 50L * 1024 * 1024;

    private const long BytesPerMegabyte = 1024L * 1024;

    private const int MaxFiles = 200;

    private static readonly System.Buffers.SearchValues<char> ForbiddenWindowsChars = System.Buffers.SearchValues.Create("<>\"|?*");

    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private const long MaxTotalBytes = 5L * 1024 * 1024;

    private const int CommitLength = 40;

    /// <summary>Downloads the folder <paramref name="path"/> of <paramref name="repoUrl"/> at <paramref name="commit"/>.</summary>
    /// <param name="repoUrl">A <c>https://github.com/owner/repo</c> URL.</param>
    /// <param name="path">The folder inside the repository; empty selects the repository root.</param>
    /// <param name="commit">The 40-character hexadecimal commit hash.</param>
    /// <param name="ct">Cancels the download.</param>
    /// <returns>The folder files, or a plain error.</returns>
    public async Task<DownloadResult> DownloadAsync(string repoUrl, string path, string commit, CancellationToken ct)
    {
        if (!TryParseRepo(repoUrl, out string owner, out string repo))
        {
            return Fail("Only https://github.com/<owner>/<repo> URLs are supported.");
        }

        if (!IsCommit(commit))
        {
            return Fail("The commit must be 40 hexadecimal characters.");
        }

        if (!TryNormalizePath(path, out string folder))
        {
            return Fail("The path is not valid.");
        }

        Uri uri = new($"https://codeload.github.com/{owner}/{repo}/zip/{commit}");
        byte[] archive;
        try
        {
            using HttpResponseMessage response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                string status = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
                return Fail($"The download failed with HTTP status {status}.");
            }

            long? declared = response.Content.Headers.ContentLength;
            if (declared > maxDownloadBytes)
            {
                return this.TooLarge();
            }

            using MemoryStream buffer = new();
            await using Stream body = await response.Content.ReadAsStreamAsync(ct);
            byte[] chunk = new byte[81920];
            int read;
            while ((read = await body.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + read > maxDownloadBytes)
                {
                    return this.TooLarge();
                }

                buffer.Write(chunk, 0, read);
            }

            archive = buffer.ToArray();
        }
        catch (HttpRequestException exception)
        {
            this.LogFailure(exception);
            return Fail("The download failed.");
        }
        catch (TaskCanceledException exception) when (!ct.IsCancellationRequested)
        {
            this.LogFailure(exception);
            return Fail("The download failed.");
        }

        return Extract(archive, folder);
    }

    /// <summary>Reads the selected folder out of the archive, enforcing the path, symlink, count and size rules.</summary>
    private static DownloadResult Extract(byte[] archive, string folder)
    {
        string prefix = folder.Length == 0 ? string.Empty : folder + "/";
        Dictionary<string, byte[]> files = new(StringComparer.Ordinal);
        HashSet<string> seenNames = new(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        try
        {
            using MemoryStream stream = new(archive, writable: false);
            using ZipArchive zip = new(stream, ZipArchiveMode.Read);
            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                string name = entry.FullName.Replace('\\', '/');
                if (!IsSafeEntryName(name))
                {
                    return Fail("The archive contains an unsafe path.");
                }

                int slash = name.IndexOf('/');
                string rest = slash < 0 ? string.Empty : name[(slash + 1)..];
                if (rest.Length == 0 || rest.EndsWith('/') || !rest.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                {
                    return Fail("The archive contains a symbolic link.");
                }

                if (!IsWindowsStorable(rest))
                {
                    return Fail("The archive contains a name that cannot be stored on Windows.");
                }

                if (!seenNames.Add(rest))
                {
                    return Fail("The archive contains two files whose names differ only in case.");
                }

                if (files.Count >= MaxFiles)
                {
                    return Fail("The skill folder has more than 200 files.");
                }

                if (entry.Length > MaxTotalBytes - total)
                {
                    return Fail("The skill folder is larger than 5 MB.");
                }

                byte[]? content = ReadBounded(entry, MaxTotalBytes - total);
                if (content is null)
                {
                    return Fail("The skill folder is larger than 5 MB.");
                }

                total += content.Length;
                files[rest[prefix.Length..]] = content;
            }
        }
        catch (InvalidDataException)
        {
            return Fail("The download is not a valid zip archive.");
        }

        return files.Count == 0
            ? Fail("Path not found at that commit.")
            : new DownloadResult(new DownloadedSkill(files), null);
    }

    /// <summary>Reads an entry, returning <see langword="null"/> when it holds more than <paramref name="limit"/> bytes.</summary>
    private static byte[]? ReadBounded(ZipArchiveEntry entry, long limit)
    {
        using Stream source = entry.Open();
        using MemoryStream buffer = new();
        byte[] chunk = new byte[16 * 1024];
        int read;
        while ((read = source.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > limit)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    /// <summary>Whether an entry name is neither rooted nor contains a <c>..</c> segment.</summary>
    private static bool IsSafeEntryName(string name)
    {
        if (name.StartsWith('/') || name.Contains(':') || name.Contains('\0'))
        {
            return false;
        }

        return !name.Split('/').Contains("..", StringComparer.Ordinal);
    }

    /// <summary>Whether every segment of a relative path can be stored on Windows: no forbidden characters, no trailing dot or space, no device name.</summary>
    private static bool IsWindowsStorable(string relativePath)
    {
        foreach (string segment in relativePath.Split('/'))
        {
            if (segment.AsSpan().IndexOfAny(ForbiddenWindowsChars) >= 0
                || segment.Any(static c => char.IsControl(c))
                || segment.EndsWith('.')
                || segment.EndsWith(' '))
            {
                return false;
            }

            int dot = segment.IndexOf('.');
            string stem = dot < 0 ? segment : segment[..dot];
            if (ReservedDeviceNames.Contains(stem.TrimEnd(' ')))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Validates the repository URL and extracts owner and repo.</summary>
    private static bool TryParseRepo(string repoUrl, out string owner, out string repo)
    {
        owner = string.Empty;
        repo = string.Empty;
        if (!Uri.TryCreate(repoUrl, UriKind.Absolute, out Uri? uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            || !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
            || !uri.IsDefaultPort
            || uri.Query.Length != 0
            || uri.Fragment.Length != 0)
        {
            return false;
        }

        string[] parts = uri.AbsolutePath.Trim('/').Split('/');
        if (parts.Length != 2)
        {
            return false;
        }

        string name = parts[1].EndsWith(".git", StringComparison.Ordinal) ? parts[1][..^4] : parts[1];
        if (!IsNameSegment(parts[0]) || !IsNameSegment(name))
        {
            return false;
        }

        owner = parts[0];
        repo = name;
        return true;
    }

    /// <summary>Whether a segment holds only letters, digits, hyphen, underscore and dot and is not a dot segment.</summary>
    private static bool IsNameSegment(string segment)
    {
        return segment.Length > 0
            && !string.Equals(segment, ".", StringComparison.Ordinal)
            && !string.Equals(segment, "..", StringComparison.Ordinal)
            && segment.All(static c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
    }

    /// <summary>Whether the text is exactly 40 hexadecimal characters.</summary>
    private static bool IsCommit(string commit)
    {
        return commit.Length == CommitLength && commit.All(static c => char.IsAsciiHexDigit(c));
    }

    /// <summary>Trims the folder path and rejects dot segments, empty segments and backslashes.</summary>
    private static bool TryNormalizePath(string path, out string folder)
    {
        folder = (path ?? string.Empty).Trim('/');
        if (folder.Length == 0)
        {
            return true;
        }

        return !folder.Contains('\\')
            && !folder.Contains('\0')
            && folder.Split('/').All(static segment => segment.Length > 0 && !string.Equals(segment, ".", StringComparison.Ordinal) && !string.Equals(segment, "..", StringComparison.Ordinal));
    }

    /// <summary>Builds a failed result.</summary>
    private static DownloadResult Fail(string error) => new(null, error);

    /// <summary>Builds the failure for an archive over the download limit.</summary>
    private DownloadResult TooLarge()
    {
        string megabytes = (maxDownloadBytes / (double)BytesPerMegabyte).ToString("0.##", CultureInfo.InvariantCulture);
        return Fail($"The download is larger than {megabytes} MB.");
    }

    /// <summary>Logs a transport failure.</summary>
    private void LogFailure(Exception exception)
    {
        logger.LogWarning(exception, "Skill archive download failed.");
    }
}
