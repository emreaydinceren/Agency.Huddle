namespace Agency.Huddle.Tests.Fakes;

/// <summary>Tests for <see cref="ScriptedReadStream"/>, the base for read-only stream fakes.</summary>
public sealed class ScriptedReadStreamTests
{
    /// <summary>A non-seekable script reports itself readable, not writable, and throws on Length, Position and Seek.</summary>
    [Fact]
    public void NonSeekable_ReportsCapabilitiesAndRefusesSeeking()
    {
        using FixedStream stream = new(canSeek: false);

        Assert.True(stream.CanRead);
        Assert.False(stream.CanWrite);
        Assert.False(stream.CanSeek);
        Assert.Throws<NotSupportedException>(() => stream.Length);
        Assert.Throws<NotSupportedException>(() => stream.Position);
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => stream.SetLength(1));
        Assert.Throws<NotSupportedException>(() => stream.Write([1], 0, 1));
    }

    /// <summary>A seekable script reports CanSeek; the scripted async read supplies the bytes.</summary>
    [Fact]
    public async Task Seekable_ReportsCanSeek_AndAsyncReadComesFromTheSubclass()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using FixedStream stream = new(canSeek: true);
        byte[] buffer = new byte[8];

        int read = await stream.ReadAsync(buffer, ct);

        Assert.True(stream.CanSeek);
        Assert.Equal(3, read);
        Assert.Equal(7, buffer[0]);
    }

    /// <summary>Flush does nothing and the synchronous read is refused unless a subclass supplies it.</summary>
    [Fact]
    public void Flush_IsNoOp_AndSyncReadThrowsByDefault()
    {
        using FixedStream stream = new(canSeek: false);

        stream.Flush();

        Assert.Throws<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
    }

    /// <summary>A script that hands back three bytes of value 7.</summary>
    /// <param name="canSeek">What the stream reports for <see cref="Stream.CanSeek"/>.</param>
    private sealed class FixedStream(bool canSeek) : ScriptedReadStream(canSeek)
    {
        /// <inheritdoc/>
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            buffer.Span[..3].Fill(7);
            return ValueTask.FromResult(3);
        }
    }
}
