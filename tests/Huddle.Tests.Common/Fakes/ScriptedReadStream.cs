namespace Agency.Huddle.Tests.Fakes;

/// <summary>
/// A read-only <see cref="Stream"/> whose reads a test scripts. Everything a fake read stream repeats is
/// here (readable, not writable, flush does nothing, the rest refuse); a subclass supplies
/// <see cref="ReadAsync(Memory{byte}, CancellationToken)"/> and, if it needs one, <see cref="Read(byte[], int, int)"/>.
/// </summary>
/// <param name="canSeek">What <see cref="CanSeek"/> reports; Length, Position and Seek still refuse unless a subclass overrides them.</param>
internal abstract class ScriptedReadStream(bool canSeek) : Stream
{
    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => canSeek;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    public abstract override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
