using System.Text;
using Agency.Huddle.SkillPacks;

namespace Agency.Huddle.Tests.SkillPacks;

/// <summary>An <see cref="IFileReader"/> over a dictionary, for library tests. Paths are made absolute with
/// <see cref="Path.GetFullPath(string)"/>, so tests can write them with either separator.</summary>
internal sealed class InMemoryFileReader : IFileReader
{
    private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> writeTimes = new(StringComparer.Ordinal);
    private readonly HashSet<string> directories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FileAttributes> attributes = new(StringComparer.Ordinal);
    private readonly HashSet<string> unreadable = new(StringComparer.Ordinal);

    /// <summary>An absolute root that works on Windows and Linux.</summary>
    public static string Root { get; } = Path.Combine(Path.GetTempPath(), "huddle-mem");

    /// <summary>The write time given to files added without one.</summary>
    public DateTimeOffset DefaultWriteTime { get; set; } = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>How many times <see cref="ReadAllText"/> and <see cref="ReadAllBytes"/> were called.</summary>
    public int Reads { get; private set; }

    /// <summary>A path under <see cref="Root"/> built from <paramref name="parts"/>.</summary>
    public static string At(params string[] parts)
    {
        return Path.GetFullPath(Path.Combine([Root, .. parts]));
    }

    /// <summary>Adds a text file.</summary>
    public InMemoryFileReader Add(string path, string text, DateTimeOffset? written = null)
    {
        return this.AddBytes(path, Encoding.UTF8.GetBytes(text), written);
    }

    /// <summary>Adds a binary file.</summary>
    public InMemoryFileReader AddBytes(string path, byte[] bytes, DateTimeOffset? written = null)
    {
        string full = Normalise(path);
        this.files[full] = bytes;
        this.writeTimes[full] = written ?? this.DefaultWriteTime;
        this.AddDirectory(Path.GetDirectoryName(full) ?? full);
        return this;
    }

    /// <summary>Adds a folder (and every parent).</summary>
    public InMemoryFileReader AddDirectory(string path)
    {
        string? current = Normalise(path);
        while (!string.IsNullOrEmpty(current) && this.directories.Add(current))
        {
            current = Path.GetDirectoryName(current);
        }

        return this;
    }

    /// <summary>Gives a path attributes, for example <see cref="FileAttributes.ReparsePoint"/>.</summary>
    public InMemoryFileReader SetAttributes(string path, FileAttributes value)
    {
        this.attributes[Normalise(path)] = value;
        return this;
    }

    /// <summary>Makes a file throw <see cref="IOException"/> when it is read.</summary>
    public InMemoryFileReader MakeUnreadable(string path)
    {
        this.unreadable.Add(Normalise(path));
        return this;
    }

    /// <summary>Removes a file.</summary>
    public InMemoryFileReader Remove(string path)
    {
        string full = Normalise(path);
        this.files.Remove(full);
        this.writeTimes.Remove(full);
        return this;
    }

    /// <inheritdoc/>
    public bool FileExists(string path)
    {
        return this.files.ContainsKey(Normalise(path));
    }

    /// <inheritdoc/>
    public bool DirectoryExists(string path)
    {
        return this.directories.Contains(Normalise(path));
    }

    /// <inheritdoc/>
    public string ReadAllText(string path)
    {
        byte[] bytes = this.ReadAllBytes(path);
        string text = Encoding.UTF8.GetString(bytes);
        return text.Length > 0 && text[0] == '﻿' ? text[1..] : text;
    }

    /// <inheritdoc/>
    public byte[] ReadAllBytes(string path)
    {
        string full = Normalise(path);
        this.Reads++;
        if (this.unreadable.Contains(full))
        {
            throw new IOException($"The file {full} is locked.");
        }

        return this.files.TryGetValue(full, out byte[]? bytes) ? bytes : throw new FileNotFoundException(full);
    }

    /// <inheritdoc/>
    public long Length(string path)
    {
        return this.files.TryGetValue(Normalise(path), out byte[]? bytes) ? bytes.LongLength : throw new FileNotFoundException(path);
    }

    /// <inheritdoc/>
    public DateTimeOffset LastWriteTime(string path)
    {
        return this.writeTimes.TryGetValue(Normalise(path), out DateTimeOffset time) ? time : this.DefaultWriteTime;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> EnumerateFiles(string directory, bool recursive)
    {
        string full = Normalise(directory);
        string prefix = full.EndsWith(Path.DirectorySeparatorChar) ? full : full + Path.DirectorySeparatorChar;
        return [.. this.files.Keys
            .Where(file => file.StartsWith(prefix, StringComparison.Ordinal)
                && (recursive || !file[prefix.Length..].Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)];
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> EnumerateDirectories(string directory)
    {
        string full = Normalise(directory);
        return [.. this.directories.Where(d => string.Equals(Path.GetDirectoryName(d), full, StringComparison.Ordinal)).Order(StringComparer.Ordinal)];
    }

    /// <inheritdoc/>
    public FileAttributes Attributes(string path)
    {
        string full = Normalise(path);
        if (this.attributes.TryGetValue(full, out FileAttributes value))
        {
            return value;
        }

        return this.directories.Contains(full) ? FileAttributes.Directory : FileAttributes.Normal;
    }

    /// <summary>The absolute form of <paramref name="path"/> with this platform's separator.</summary>
    private static string Normalise(string path)
    {
        string full = Path.GetFullPath(path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar));
        return full.Length > 1 && full.EndsWith(Path.DirectorySeparatorChar) && Path.GetPathRoot(full) != full ? full.TrimEnd(Path.DirectorySeparatorChar) : full;
    }
}
