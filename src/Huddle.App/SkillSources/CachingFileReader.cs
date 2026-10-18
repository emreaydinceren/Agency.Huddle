using System.Collections.Concurrent;
using Agency.Huddle.SkillPacks;
using Agency.Huddle.SkillPacks.Folders;

namespace Agency.Huddle.App.SkillSources;

/// <summary>An <see cref="IFileReader"/> over the disk with the two caches §6.5 names: file text by path, keyed by write
/// time and length, and folder hash by path, keyed by the folder's newest write time and file count (§8.4, "When hashing
/// runs"). A rebuild scope drops entries; Reload drops all.</summary>
internal sealed class CachingFileReader : IFileReader, IFolderHasher
{
    private readonly IFileReader inner;
    private readonly ConcurrentDictionary<string, (long Length, DateTimeOffset Written, string Text)> texts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, (DateTimeOffset Newest, int Count, string Hash)> hashes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Wraps <paramref name="inner"/>, the disk by default.</summary>
    public CachingFileReader(IFileReader? inner = null)
    {
        this.inner = inner ?? PhysicalFileReader.Instance;
    }

    /// <summary>How many folders were hashed rather than served from the cache.</summary>
    public int HashesComputed { get; private set; }

    /// <summary>Drops the cache entries a scope names.</summary>
    public void Invalidate(RebuildScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (scope.Everything)
        {
            this.texts.Clear();
            this.hashes.Clear();
            return;
        }

        foreach (string file in scope.Files)
        {
            this.texts.TryRemove(file, out _);
        }

        foreach (string folder in scope.SkillFolders)
        {
            foreach (string key in this.hashes.Keys.Where(key => key.StartsWith(folder, StringComparison.OrdinalIgnoreCase)))
            {
                this.hashes.TryRemove(key, out _);
            }
        }
    }

    /// <inheritdoc/>
    public bool FileExists(string path) => this.inner.FileExists(path);

    /// <inheritdoc/>
    public bool DirectoryExists(string path) => this.inner.DirectoryExists(path);

    /// <inheritdoc/>
    public string ReadAllText(string path)
    {
        long length = this.inner.Length(path);
        DateTimeOffset written = this.inner.LastWriteTime(path);
        if (this.texts.TryGetValue(path, out (long Length, DateTimeOffset Written, string Text) cached) && cached.Length == length && cached.Written == written)
        {
            return cached.Text;
        }

        string text = this.inner.ReadAllText(path);
        this.texts[path] = (length, written, text);
        return text;
    }

    /// <inheritdoc/>
    public byte[] ReadAllBytes(string path) => this.inner.ReadAllBytes(path);

    /// <inheritdoc/>
    public long Length(string path) => this.inner.Length(path);

    /// <inheritdoc/>
    public DateTimeOffset LastWriteTime(string path) => this.inner.LastWriteTime(path);

    /// <inheritdoc/>
    public IReadOnlyList<string> EnumerateFiles(string directory, bool recursive) => this.inner.EnumerateFiles(directory, recursive);

    /// <inheritdoc/>
    public IReadOnlyList<string> EnumerateDirectories(string directory) => this.inner.EnumerateDirectories(directory);

    /// <inheritdoc/>
    public FileAttributes Attributes(string path) => this.inner.Attributes(path);

    /// <inheritdoc/>
    public string HashFolder(string folder)
    {
        IReadOnlyList<string> files = this.inner.EnumerateFiles(folder, recursive: true);
        DateTimeOffset newest = files.Count == 0 ? DateTimeOffset.MinValue : files.Max(this.inner.LastWriteTime);
        if (this.hashes.TryGetValue(folder, out (DateTimeOffset Newest, int Count, string Hash) cached) && cached.Newest == newest && cached.Count == files.Count)
        {
            return cached.Hash;
        }

        string hash = SkillFolderHash.ComputeFolder(this.inner, folder);
        this.HashesComputed++;
        this.hashes[folder] = (newest, files.Count, hash);
        return hash;
    }
}
