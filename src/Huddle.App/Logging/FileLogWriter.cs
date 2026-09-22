using System.Globalization;

namespace Agency.Huddle.App.Logging;

/// <summary>
/// The one file that every <see cref="FileLogger"/> from a single <see cref="FileLoggerProvider"/>
/// writes to: created once per app run, appended to under a lock, and flushed on every write.
/// </summary>
/// <remarks>
/// <para>
/// Flushing on every write is deliberate, and is why this wraps an unbuffered
/// <see cref="StreamWriter"/> rather than batching. The failures worth opening this file for - a
/// configuration value that will not bind to its option type, a port already in use - terminate the
/// process from inside <c>builder.Build()</c> or <c>app.Run()</c> without unwinding to a flush, so a
/// buffer would drop precisely the last lines that explain the exit.
/// </para>
/// <para>
/// The file name embeds the start time and the process id, so two runs never collide even when they
/// start in the same second, and an ordinary ordinal sort of the names is also a sort by age - which
/// is what <see cref="Prune"/> relies on.
/// </para>
/// </remarks>
internal sealed class FileLogWriter : IDisposable
{
    private readonly Lock gate = new();
    private readonly StreamWriter writer;
    private bool disposed;

    /// <summary>
    /// Creates this run's log file, writes its header, and prunes older files in the same directory.
    /// </summary>
    /// <param name="directoryPath">Directory to write into. Created when it does not exist.</param>
    /// <param name="retainedFileCount">How many files to keep, this one included.</param>
    /// <param name="environmentName">Host environment name, recorded in the header.</param>
    /// <param name="startedAt">When this run started. Names the file and heads its first lines.</param>
    public FileLogWriter(string directoryPath, int retainedFileCount, string environmentName, DateTimeOffset startedAt)
    {
        Directory.CreateDirectory(directoryPath);

        var processId = Environment.ProcessId;
        var fileName = string.Create(
            CultureInfo.InvariantCulture,
            $"huddle-{startedAt:yyyyMMdd-HHmmss}-{processId}.log");

        this.FilePath = Path.Combine(directoryPath, fileName);
        this.writer = new StreamWriter(this.FilePath, append: false) { AutoFlush = true };

        this.writer.WriteLine("# Agency.Huddle run log");
        this.writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"# started     {startedAt:yyyy-MM-dd HH:mm:ss.fff zzz}"));
        this.writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"# process     {processId}"));
        this.writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"# environment {environmentName}"));
        this.writer.WriteLine();

        Prune(directoryPath, retainedFileCount);
    }

    /// <summary>The full path of the file this run is writing to.</summary>
    public string FilePath { get; }

    /// <summary>
    /// Appends one already-formatted entry, which may span several lines, followed by a line break.
    /// </summary>
    /// <param name="entry">The text to append.</param>
    public void Write(string entry)
    {
        lock (this.gate)
        {
            // A logger handed out by this provider can outlive it during host shutdown, when
            // providers are disposed before the last framework log calls are made. Dropping those
            // lines is right; throwing ObjectDisposedException out of a log call is not.
            if (this.disposed)
            {
                return;
            }

            this.writer.WriteLine(entry);
        }
    }

    /// <summary>Closes the file. Later calls to <see cref="Write"/> are ignored.</summary>
    public void Dispose()
    {
        lock (this.gate)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            this.writer.Dispose();
        }
    }

    /// <summary>
    /// Deletes the oldest files until at most <paramref name="retainedFileCount"/> remain.
    /// </summary>
    /// <param name="directoryPath">Directory holding this run's log files.</param>
    /// <param name="retainedFileCount">How many to keep. Zero or less keeps every file.</param>
    private static void Prune(string directoryPath, int retainedFileCount)
    {
        if (retainedFileCount <= 0)
        {
            return;
        }

        try
        {
            string[] files = Directory.GetFiles(directoryPath, "huddle-*.log");
            if (files.Length <= retainedFileCount)
            {
                return;
            }

            // The name carries yyyyMMdd-HHmmss, so ordinal order is age order - no file timestamps
            // to stat, and no dependence on a copy or a restore having preserved them.
            Array.Sort(files, StringComparer.Ordinal);

            for (var i = 0; i < files.Length - retainedFileCount; i++)
            {
                File.Delete(files[i]);
            }
        }
        catch (IOException)
        {
            // A file another run still holds open cannot be deleted, and retention is housekeeping -
            // never a reason to fail a start-up. The next run finds it closed and removes it then.
        }
        catch (UnauthorizedAccessException)
        {
            // As above: a read-only or permission-denied file is left where it is rather than
            // turning "write my log" into "refuse to start".
        }
    }
}