using System.Buffers;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Agency.Huddle.Acp.DotAcp;

/// <summary>
/// A read-only <see cref="Stream"/> wrapper over an agent's newline-delimited JSON-RPC output that
/// rewrites one thing: an inbound <em>request</em> whose top-level <c>method</c> is exactly
/// <c>elicitation/create</c> (and that carries an <c>id</c>) comes out as <c>_elicitation/create</c>.
/// Stable dotacp routes only methods that start with an underscore to
/// <c>IAcpClient.ExtMethodAsync</c> and answers a bare <c>elicitation/create</c> with -32601, so this
/// is how the host receives the request at all.
/// </summary>
/// <remarks>
/// The wrapper buffers up to a newline and decides per line. A cheap byte search filters out almost
/// every line; only a line that contains the method name is parsed, and only a request with exactly
/// that method is re-serialised. Every other line is forwarded byte for byte - a
/// <c>session/update</c> text chunk may legitimately contain the string, so a byte replace is never
/// safe. A final line with no trailing newline is flushed at end of stream.
/// </remarks>
internal sealed class NdjsonMethodRewriteStream : Stream
{
    private const string FromMethod = "elicitation/create";

    private const string ToMethod = "_elicitation/create";

    private const int ReadBufferSize = 4096;

    private static readonly JsonSerializerOptions WriteOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly Stream inner;

    private readonly byte[] readBuffer = new byte[NdjsonMethodRewriteStream.ReadBufferSize];

    private readonly ArrayBufferWriter<byte> partialLine = new();

    private readonly ArrayBufferWriter<byte> ready = new();

    private int readyPosition;

    private bool ended;

    private bool disposed;

    /// <summary>Wraps <paramref name="inner"/>, which this stream then owns and disposes with itself.</summary>
    /// <param name="inner">The agent process's standard output.</param>
    internal NdjsonMethodRewriteStream(Stream inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        this.inner = inner;
    }

    public override bool CanRead => this.inner.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    private bool HasReady => this.readyPosition < this.ready.WrittenCount;

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return this.Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        if (buffer.IsEmpty)
        {
            return 0;
        }

        while (!this.HasReady)
        {
            if (this.ended)
            {
                return 0;
            }

            this.Absorb(this.inner.Read(this.readBuffer, 0, this.readBuffer.Length));
        }

        return this.Drain(buffer);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        if (buffer.IsEmpty)
        {
            return 0;
        }

        while (!this.HasReady)
        {
            if (this.ended)
            {
                return 0;
            }

            this.Absorb(await this.inner.ReadAsync(this.readBuffer.AsMemory(), cancellationToken).ConfigureAwait(false));
        }

        return this.Drain(buffer.Span);
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
        if (!this.disposed)
        {
            this.disposed = true;
            await this.inner.DisposeAsync().ConfigureAwait(false);
        }

        await base.DisposeAsync().ConfigureAwait(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !this.disposed)
        {
            this.disposed = true;
            this.inner.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>Re-serialises <paramref name="body"/> with its method renamed when it is an <c>elicitation/create</c> request.</summary>
    /// <param name="body">One line without its terminator.</param>
    /// <param name="rewritten">The replacement bytes when this returns true.</param>
    /// <returns>True when the line is a request with exactly the method <c>elicitation/create</c>.</returns>
    private static bool TryRewrite(ReadOnlySpan<byte> body, out byte[] rewritten)
    {
        rewritten = [];
        if (body.IndexOf("elicitation/create"u8) < 0)
        {
            return false;
        }

        JsonObject? message;
        try
        {
            message = JsonNode.Parse(body) as JsonObject;
        }
        catch (JsonException)
        {
            // Not JSON at all (the adapter never writes this, but a stray line must pass through untouched).
            return false;
        }

        if (message is null
            || !message.ContainsKey("id")
            || message["method"] is not JsonValue method
            || !method.TryGetValue(out string? name)
            || !string.Equals(name, NdjsonMethodRewriteStream.FromMethod, StringComparison.Ordinal))
        {
            return false;
        }

        message["method"] = NdjsonMethodRewriteStream.ToMethod;
        rewritten = JsonSerializer.SerializeToUtf8Bytes(message, NdjsonMethodRewriteStream.WriteOptions);
        return true;
    }

    /// <summary>Takes the result of one inner read: end of stream flushes the unfinished line, anything else is split into lines.</summary>
    /// <param name="read">The count of bytes now in the read buffer, or 0 at end of stream.</param>
    private void Absorb(int read)
    {
        if (read == 0)
        {
            this.ended = true;
            if (this.partialLine.WrittenCount > 0)
            {
                this.Emit(this.partialLine.WrittenSpan);
                this.partialLine.Clear();
            }

            return;
        }

        ReadOnlySpan<byte> chunk = this.readBuffer.AsSpan(0, read);
        while (!chunk.IsEmpty)
        {
            int newline = chunk.IndexOf((byte)'\n');
            if (newline < 0)
            {
                this.partialLine.Write(chunk);
                return;
            }

            this.partialLine.Write(chunk[..(newline + 1)]);
            this.Emit(this.partialLine.WrittenSpan);
            this.partialLine.Clear();
            chunk = chunk[(newline + 1)..];
        }
    }

    /// <summary>Queues one complete line (with its terminator, if it had one), rewritten when it is an <c>elicitation/create</c> request.</summary>
    /// <param name="line">The line, including any <c>\n</c> or <c>\r\n</c>.</param>
    private void Emit(ReadOnlySpan<byte> line)
    {
        ReadOnlySpan<byte> body = line;
        if (body.EndsWith("\n"u8))
        {
            body = body[..^1];
        }

        if (body.EndsWith("\r"u8))
        {
            body = body[..^1];
        }

        if (NdjsonMethodRewriteStream.TryRewrite(body, out byte[] rewritten))
        {
            this.ready.Write(rewritten);
            this.ready.Write(line[body.Length..]);
        }
        else
        {
            this.ready.Write(line);
        }
    }

    /// <summary>Copies as much queued output as fits into <paramref name="destination"/>.</summary>
    /// <param name="destination">The caller's buffer.</param>
    /// <returns>The count of bytes copied.</returns>
    private int Drain(Span<byte> destination)
    {
        ReadOnlySpan<byte> pending = this.ready.WrittenSpan[this.readyPosition..];
        int count = Math.Min(pending.Length, destination.Length);
        pending[..count].CopyTo(destination);
        this.readyPosition += count;

        if (!this.HasReady)
        {
            this.ready.Clear();
            this.readyPosition = 0;
        }

        return count;
    }
}
