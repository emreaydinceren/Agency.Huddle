using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Agency.Huddle.Acp.DotAcp;

/// <summary>
/// A read-only <see cref="Stream"/> wrapper over an agent's newline-delimited JSON-RPC output. It
/// does two things per line and otherwise forwards every byte unchanged:
/// it counts each <c>session/update</c> notification into a <see cref="WireOrderLedger"/>, and, when
/// <c>rewriteElicitation</c> is set, it renames an inbound <em>request</em> whose top-level
/// <c>method</c> is exactly <c>elicitation/create</c> (and that carries an <c>id</c>) to
/// <c>_elicitation/create</c>. Stable dotacp routes only methods that start with an underscore to
/// <c>IAcpClient.ExtMethodAsync</c> and answers a bare <c>elicitation/create</c> with -32601, so the
/// rename is how the host receives the request at all. When <c>rewriteNotices</c> is set, it also renames
/// a <c>session/update</c> notification whose <c>params.update.sessionUpdate</c> is exactly <c>notice</c> to
/// <c>_session/notice</c>: stable dotacp predates that update kind and throws on its discriminator, so the
/// rename routes it to <c>IAcpClient.ExtNotificationAsync</c> instead. The line is still counted as a
/// <c>session/update</c>, and the client adapter marks it dispatched, so the wire-order barrier holds.
/// </summary>
/// <remarks>
/// <para>
/// The wrapper buffers up to a newline and decides per line. A cheap byte search filters out almost
/// every line; only a line that contains a method name is parsed. Every other line is forwarded byte
/// for byte - a <c>session/update</c> text chunk may legitimately contain either string, so a byte
/// match or replace is never safe. A final line with no trailing newline is flushed at end of stream.
/// </para>
/// <para>
/// <b>Counting is the wire-order barrier's half of the bargain.</b> A line is counted when it is
/// queued for the reader, before any later line can be consumed, so by the time StreamJsonRpc sees a
/// <c>session/prompt</c> response every update the agent wrote ahead of it is already in the ledger.
/// Only a notification (no <c>id</c>) whose top-level <c>method</c> is exactly <c>session/update</c>
/// and whose <c>params.sessionId</c> is a string is counted. Those lines are read with
/// <see cref="Utf8JsonReader"/> and never materialised, since every streamed chunk is one.
/// </para>
/// </remarks>
internal sealed class NdjsonMethodRewriteStream : Stream
{
    private const string FromMethod = "elicitation/create";

    private const string ToMethod = "_elicitation/create";

    private const string NoticeUpdateKind = "notice";

    private const string NoticeMethod = "_session/notice";

    private const int ReadBufferSize = 4096;

    private static readonly JsonSerializerOptions WriteOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly Stream inner;

    private readonly WireOrderLedger ledger;

    private readonly bool rewriteElicitation;

    private readonly bool rewriteNotices;

    private readonly byte[] readBuffer = new byte[NdjsonMethodRewriteStream.ReadBufferSize];

    private readonly ArrayBufferWriter<byte> partialLine = new();

    private readonly ArrayBufferWriter<byte> ready = new();

    private int readyPosition;

    private bool ended;

    private bool disposed;

    /// <summary>Wraps <paramref name="inner"/>, which this stream then owns and disposes with itself.</summary>
    /// <param name="inner">The agent process's standard output.</param>
    /// <param name="ledger">Receives one <see cref="WireOrderLedger.NoteRead"/> per <c>session/update</c> notification line.</param>
    /// <param name="rewriteElicitation">True to rename inbound <c>elicitation/create</c> requests; false to forward them unchanged.</param>
    /// <param name="rewriteNotices">True to rename inbound <c>notice</c> session updates to <c>_session/notice</c>; false to forward them unchanged.</param>
    internal NdjsonMethodRewriteStream(Stream inner, WireOrderLedger ledger, bool rewriteElicitation, bool rewriteNotices = false)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(ledger);
        this.inner = inner;
        this.ledger = ledger;
        this.rewriteElicitation = rewriteElicitation;
        this.rewriteNotices = rewriteNotices;
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

    /// <summary>Re-serialises <paramref name="body"/> with its method renamed when it is a <c>notice</c> session update.</summary>
    /// <param name="body">One line without its terminator, already known to be a counted <c>session/update</c> notification.</param>
    /// <param name="rewritten">The replacement bytes when this returns true.</param>
    /// <returns>True when the line's <c>params.update.sessionUpdate</c> is exactly <c>notice</c>.</returns>
    private static bool TryRewriteNotice(ReadOnlySpan<byte> body, out byte[] rewritten)
    {
        rewritten = [];

        // Most counted lines are streamed text chunks; only one that carries the word at all is parsed.
        if (body.IndexOf("\"notice\""u8) < 0)
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
            // The line was already read as JSON to count it, so this is unreachable; forward untouched.
            return false;
        }

        if (message?["params"]?["update"]?["sessionUpdate"] is not JsonValue kind
            || !kind.TryGetValue(out string? name)
            || !string.Equals(name, NdjsonMethodRewriteStream.NoticeUpdateKind, StringComparison.Ordinal))
        {
            return false;
        }

        message["method"] = NdjsonMethodRewriteStream.NoticeMethod;
        rewritten = JsonSerializer.SerializeToUtf8Bytes(message, NdjsonMethodRewriteStream.WriteOptions);
        return true;
    }

    /// <summary>Reads the session id of a line that is a <c>session/update</c> notification.</summary>
    /// <param name="body">One line without its terminator.</param>
    /// <param name="sessionId">The line's <c>params.sessionId</c> when this returns true.</param>
    /// <returns>True when the line is a JSON-RPC notification (no <c>id</c>) whose top-level <c>method</c> is exactly <c>session/update</c> and whose <c>params.sessionId</c> is a string.</returns>
    private static bool TryReadSessionUpdate(ReadOnlySpan<byte> body, [NotNullWhen(true)] out string? sessionId)
    {
        sessionId = null;
        if (body.IndexOf("session/update"u8) < 0)
        {
            return false;
        }

        bool isUpdateMethod = false;
        bool hasId = false;
        string? found = null;
        try
        {
            Utf8JsonReader reader = new(body);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return false;
            }

            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                if (reader.ValueTextEquals("method"u8))
                {
                    _ = reader.Read();
                    isUpdateMethod = reader.TokenType == JsonTokenType.String && reader.ValueTextEquals("session/update"u8);
                }
                else if (reader.ValueTextEquals("id"u8))
                {
                    hasId = true;
                    _ = reader.Read();
                    reader.Skip();
                }
                else if (reader.ValueTextEquals("params"u8))
                {
                    _ = reader.Read();
                    found = NdjsonMethodRewriteStream.ReadSessionId(ref reader);
                }
                else
                {
                    _ = reader.Read();
                    reader.Skip();
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON (or cut short): an uncounted line is forwarded untouched like any other.
            return false;
        }

        sessionId = found;
        return isUpdateMethod && !hasId && found is not null;
    }

    /// <summary>Reads the <c>sessionId</c> string out of a <c>params</c> value, leaving the reader on the value's last token.</summary>
    /// <param name="reader">A reader positioned on the first token of the <c>params</c> value.</param>
    /// <returns>The session id, or null when <c>params</c> is not an object or has no string <c>sessionId</c>.</returns>
    private static string? ReadSessionId(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return null;
        }

        string? found = null;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            bool isSessionId = reader.ValueTextEquals("sessionId"u8);
            _ = reader.Read();
            if (isSessionId && reader.TokenType == JsonTokenType.String)
            {
                found = reader.GetString();
            }
            else
            {
                reader.Skip();
            }
        }

        return found;
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

        bool isSessionUpdate = false;
        if (NdjsonMethodRewriteStream.TryReadSessionUpdate(body, out string? sessionId))
        {
            this.ledger.NoteRead(sessionId);
            isSessionUpdate = true;
        }

        if (isSessionUpdate && this.rewriteNotices && NdjsonMethodRewriteStream.TryRewriteNotice(body, out byte[] notice))
        {
            this.ready.Write(notice);
            this.ready.Write(line[body.Length..]);
        }
        else if (this.rewriteElicitation && NdjsonMethodRewriteStream.TryRewrite(body, out byte[] rewritten))
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
