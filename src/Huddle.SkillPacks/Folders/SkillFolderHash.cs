using System.Security.Cryptography;
using System.Text;

namespace Agency.Huddle.SkillPacks.Folders;

/// <summary>
/// Computes the stable SHA-256 of a skill folder recorded in <c>.source.json</c> and compared to detect
/// "modified locally" (SK-7, SK-9, NF-4).
/// </summary>
public static class SkillFolderHash
{
    private const string SourceFileName = ".source.json";

    private static readonly string[] TextExtensions = [".md", ".txt", ".json", ".yml", ".yaml"];

    /// <summary>
    /// Hashes the given files. <c>.source.json</c> is skipped; files are ordered by relative path
    /// (ordinal); each contributes its path, NUL, decimal length, NUL and bytes. CRLF is normalised to LF
    /// for known text extensions before hashing.
    /// </summary>
    /// <param name="files">File bytes keyed by path relative to the skill folder, using <c>/</c> separators.</param>
    /// <returns>The lowercase hex SHA-256.</returns>
    public static string Compute(IReadOnlyDictionary<string, byte[]> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string path in files.Keys.Where(static key => !string.Equals(key, SourceFileName, StringComparison.Ordinal) && !IsInGitFolder(key)).Order(StringComparer.Ordinal))
        {
            byte[] content = Normalize(path, files[path]);
            hash.AppendData(Encoding.UTF8.GetBytes(path));
            hash.AppendData([0]);
            hash.AppendData(Encoding.UTF8.GetBytes(content.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            hash.AppendData([0]);
            hash.AppendData(content);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    /// <summary>Reads every file under <paramref name="folder"/> recursively and hashes it with <see cref="Compute"/>.</summary>
    /// <param name="folder">The skill folder on disk.</param>
    /// <returns>The lowercase hex SHA-256.</returns>
    public static string ComputeFolder(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        return ComputeFolder(PhysicalFileReader.Instance, folder);
    }

    /// <summary>Reads every file under <paramref name="folder"/> through <paramref name="reader"/>, skipping any
    /// <c>.git</c> folder, and hashes it with <see cref="Compute"/>.</summary>
    /// <param name="reader">The reader the caller uses for the rest of its work.</param>
    /// <param name="folder">The skill folder.</param>
    /// <returns>The lowercase hex SHA-256.</returns>
    public static string ComputeFolder(IFileReader reader, string folder)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        Dictionary<string, byte[]> files = new(StringComparer.Ordinal);
        foreach (string file in reader.EnumerateFiles(folder, recursive: true))
        {
            string relative = Path.GetRelativePath(folder, file).Replace('\\', '/');
            if (!IsInGitFolder(relative))
            {
                files[relative] = reader.ReadAllBytes(file);
            }
        }

        return Compute(files);
    }

    /// <summary>Each file's SHA-256 by relative path (with <c>/</c>), normalised as <see cref="Compute"/> normalises it, with
    /// <c>.source.json</c> and any <c>.git</c> folder left out; empty for a folder that does not exist.</summary>
    public static IReadOnlyDictionary<string, string> FileHashes(IFileReader reader, string folder)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        Dictionary<string, string> hashes = new(StringComparer.Ordinal);
        if (!reader.DirectoryExists(folder))
        {
            return hashes;
        }

        foreach (string file in reader.EnumerateFiles(folder, recursive: true))
        {
            string relative = Path.GetRelativePath(folder, file).Replace('\\', '/');
            if (!IsInGitFolder(relative) && !string.Equals(relative, SourceFileName, StringComparison.Ordinal))
            {
                hashes[relative] = Convert.ToHexStringLower(SHA256.HashData(Normalize(relative, reader.ReadAllBytes(file))));
            }
        }

        return hashes;
    }

    /// <summary>How many files differ between two folders: changed content, plus files on one side only.</summary>
    public static int ChangedFiles(IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        int changed = before.Count(pair => !after.TryGetValue(pair.Key, out string? hash) || !string.Equals(hash, pair.Value, StringComparison.Ordinal));
        return changed + after.Keys.Count(key => !before.ContainsKey(key));
    }

    /// <summary>Whether a relative path lies in a <c>.git</c> folder, for a Skill that is the root of its own clone.</summary>
    private static bool IsInGitFolder(string relative)
    {
        return relative.StartsWith(".git/", StringComparison.Ordinal) || relative.Contains("/.git/", StringComparison.Ordinal);
    }

    private static byte[] Normalize(string path, byte[] content)
    {
        string extension = Path.GetExtension(path);
        if (!TextExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return content;
        }

        List<byte> result = new(content.Length);
        for (int index = 0; index < content.Length; index++)
        {
            bool crlf = content[index] == (byte)'\r' && index + 1 < content.Length && content[index + 1] == (byte)'\n';
            if (!crlf)
            {
                result.Add(content[index]);
            }
        }

        return [.. result];
    }
}
