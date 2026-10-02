using System.Text;

namespace Agency.Huddle.Acp.Tests.DotAcp;

/// <summary>
/// A read-only test stream over an inner stream that hands back at most <c>maxChunk</c> bytes per
/// read and records whether it was disposed, synchronously or asynchronously.
/// </summary>
internal sealed class ChunkedReadStream : Stream
{
    private readonly Stream inner;

    private readonly int maxChunk;

    private readonly TaskCompletionSource disposedSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int readCount;

    /// <summary>Wraps <paramref name="inner"/>, capping every read at <paramref name="maxChunk"/> bytes.</summary>
    /// <param name="inner">The stream to read from.</param>
    /// <param name="maxChunk">The most bytes any single read returns.</param>
    internal ChunkedReadStream(Stream inner, int maxChunk)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxChunk, 1);
        this.inner = inner;
        this.maxChunk = maxChunk;
    }

    /// <summary>Gets a value indicating whether <see cref="Dispose(bool)"/> ran.</summary>
    internal bool Disposed { get; private set; }

    /// <summary>Gets a value indicating whether <see cref="DisposeAsync"/> ran.</summary>
    internal bool DisposedAsync { get; private set; }

    /// <summary>Gets a task that completes the first time this stream is disposed, either way. The JSON-RPC layer releases its streams on its own schedule, so a test awaits this rather than checking a flag at one instant.</summary>
    internal Task WhenDisposed => this.disposedSignal.Task;

    /// <summary>Gets how many reads, of any overload, reached this stream.</summary>
    internal int ReadCount => Volatile.Read(ref this.readCount);

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>Builds a stream over the UTF-8 bytes of <paramref name="text"/>.</summary>
    /// <param name="text">The content to serve.</param>
    /// <param name="maxChunk">The most bytes any single read returns.</param>
    /// <returns>The stream.</returns>
    internal static ChunkedReadStream FromText(string text, int maxChunk)
    {
        return new ChunkedReadStream(new MemoryStream(Encoding.UTF8.GetBytes(text)), maxChunk);
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        _ = Interlocked.Increment(ref this.readCount);
        return this.inner.Read(buffer, offset, Math.Min(count, this.maxChunk));
    }

    public override int Read(Span<byte> buffer)
    {
        _ = Interlocked.Increment(ref this.readCount);
        return this.inner.Read(buffer[..Math.Min(buffer.Length, this.maxChunk)]);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        _ = Interlocked.Increment(ref this.readCount);
        return this.inner.ReadAsync(buffer[..Math.Min(buffer.Length, this.maxChunk)], cancellationToken);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override async ValueTask DisposeAsync()
    {
        this.DisposedAsync = true;
        this.disposedSignal.TrySetResult();
        await this.inner.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.Disposed = true;
            this.disposedSignal.TrySetResult();
            this.inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
