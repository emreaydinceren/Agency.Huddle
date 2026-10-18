namespace Agency.Huddle.SkillPacks;

/// <summary>An <see cref="IFileReader"/> over the real disk.</summary>
public sealed class PhysicalFileReader : IFileReader
{
    /// <summary>The one instance; the type holds no state.</summary>
    public static PhysicalFileReader Instance { get; } = new();

    /// <inheritdoc/>
    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    /// <inheritdoc/>
    public bool DirectoryExists(string path)
    {
        return Directory.Exists(path);
    }

    /// <inheritdoc/>
    public string ReadAllText(string path)
    {
        return File.ReadAllText(path);
    }

    /// <inheritdoc/>
    public byte[] ReadAllBytes(string path)
    {
        return File.ReadAllBytes(path);
    }

    /// <inheritdoc/>
    public long Length(string path)
    {
        return new FileInfo(path).Length;
    }

    /// <inheritdoc/>
    public DateTimeOffset LastWriteTime(string path)
    {
        return File.GetLastWriteTimeUtc(path);
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> EnumerateFiles(string directory, bool recursive)
    {
        List<string> files = [];
        Collect(directory, recursive, files);
        files.Sort(StringComparer.Ordinal);
        return files;
    }

    /// <summary>Adds the files of <paramref name="directory"/> and, when <paramref name="recursive"/>, of its folders,
    /// never descending into a link or junction (S004 reports those).</summary>
    private static void Collect(string directory, bool recursive, List<string> files)
    {
        EnumerationOptions options = new() { IgnoreInaccessible = true, AttributesToSkip = 0 };
        files.AddRange(Directory.GetFiles(directory, "*", options));
        if (!recursive)
        {
            return;
        }

        foreach (string child in Directory.GetDirectories(directory, "*", options))
        {
            if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0)
            {
                Collect(child, recursive, files);
            }
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> EnumerateDirectories(string directory)
    {
        EnumerationOptions options = new() { IgnoreInaccessible = true, AttributesToSkip = 0 };
        string[] directories = Directory.GetDirectories(directory, "*", options);
        Array.Sort(directories, StringComparer.Ordinal);
        return directories;
    }

    /// <inheritdoc/>
    public FileAttributes Attributes(string path)
    {
        return File.GetAttributes(path);
    }
}
