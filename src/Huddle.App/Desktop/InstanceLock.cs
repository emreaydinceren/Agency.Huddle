namespace Agency.Huddle.App.Desktop;

/// <summary>
/// An exclusive, process-wide lock on a data directory that enforces a single running instance.
/// The lock is an open <see cref="FileStream"/> on <c>instance.lock</c> with <see cref="FileShare.None"/>.
/// </summary>
/// <remarks>
/// .NET on Unix implements <see cref="FileShare.None"/> with <c>flock</c>, so this works across
/// processes on macOS and Linux as well as on Windows. On Windows sharing is per handle; on Unix .NET
/// takes <c>flock</c> per open file description, so a second open in the same process fails on both.
/// The lock file is deliberately never deleted: deleting it would race a new owner. The OS releases
/// the lock when the process dies.
/// </remarks>
internal sealed class InstanceLock : IDisposable
{
    private readonly FileStream stream;

    private InstanceLock(FileStream stream)
    {
        this.stream = stream;
    }

    /// <summary>
    /// Tries to take the instance lock for <paramref name="dataDir"/>, creating the directory if needed.
    /// </summary>
    /// <param name="dataDir">The data directory that holds <c>instance.lock</c>.</param>
    /// <returns>The held lock, or <see langword="null"/> when another owner already holds it.</returns>
    internal static InstanceLock? TryAcquire(string dataDir)
    {
        Directory.CreateDirectory(dataDir);
        string path = Path.Combine(dataDir, "instance.lock");

        try
        {
            FileStream stream = new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return new InstanceLock(stream);
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>Releases the lock. Calling it more than once is harmless.</summary>
    public void Dispose()
    {
        this.stream.Dispose();
    }
}
