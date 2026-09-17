namespace Agency.Huddle.MockAdapter;

/// <summary>Presents a separate input and output stream as one duplex <see cref="Stream"/>.</summary>
internal sealed class DuplexStream : Stream
{
    private readonly Stream input;

    private readonly Stream output;

    private readonly bool leaveOpen;

    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DuplexStream"/> class, reading from
    /// <paramref name="input"/> and writing to <paramref name="output"/>.
    /// </summary>
    /// <param name="input">The half-stream reads are delegated to.</param>
    /// <param name="output">The half-stream writes and flushes are delegated to.</param>
    /// <param name="leaveOpen">When true, disposing this instance disposes neither half.</param>
    internal DuplexStream(Stream input, Stream output, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        this.input = input;
        this.output = output;
        this.leaveOpen = leaveOpen;
    }

    /// <summary>
    /// Gets a value indicating whether the stream can be read. Per the <see cref="Stream"/>
    /// contract, this reports <see langword="false"/> once disposed rather than throwing — a
    /// closed stream answers this probe, it does not fault on it, and a dispose-path helper
    /// (a <see cref="StreamReader"/>/<see cref="StreamWriter"/>, or framework teardown code) is
    /// entitled to call it after disposal.
    /// </summary>
    public override bool CanRead => !this.disposed;

    /// <summary>
    /// Gets a value indicating whether the stream can be written. See <see cref="CanRead"/> for
    /// why this does not throw once disposed.
    /// </summary>
    public override bool CanWrite => !this.disposed;

    /// <summary>Always <see langword="false"/> — a duplex stream cannot seek, disposed or not.</summary>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override long Length
    {
        get
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            throw new NotSupportedException("A duplex stream has no defined length.");
        }
    }

    /// <inheritdoc/>
    public override long Position
    {
        get
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            throw new NotSupportedException("A duplex stream has no defined position.");
        }

        set
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            throw new NotSupportedException("A duplex stream has no defined position.");
        }
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        return this.input.Read(buffer, offset, count);
    }

    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        return this.input.ReadAsync(buffer, cancellationToken);
    }

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        this.output.Write(buffer, offset, count);
    }

    /// <inheritdoc/>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        return this.output.WriteAsync(buffer, cancellationToken);
    }

    /// <inheritdoc/>
    public override void Flush()
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        this.output.Flush();
    }

    /// <inheritdoc/>
    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        return this.output.FlushAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        throw new NotSupportedException("A duplex stream cannot seek.");
    }

    /// <inheritdoc/>
    public override void SetLength(long value)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        throw new NotSupportedException("A duplex stream cannot be resized.");
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;

        if (disposing && !this.leaveOpen)
        {
            this.input.Dispose();
            this.output.Dispose();
        }

        base.Dispose(disposing);
    }
}
