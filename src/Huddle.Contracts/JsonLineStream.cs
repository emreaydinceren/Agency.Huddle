using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace Agency.Huddle.Contracts;

[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "JsonLineStream is the exact type name mandated by Team-Specifications.md §6.1; renaming would break the normative wire-protocol API.")]
public sealed class JsonLineStream : IAsyncDisposable
{
    public const int MaxLineBytes = 1024 * 1024;

    private readonly Stream stream;
    private readonly bool leaveOpen;
    private readonly StreamReader reader;
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private bool disposed;

    public JsonLineStream(Stream stream, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        this.stream = stream;
        this.leaveOpen = leaveOpen;
        this.reader = new StreamReader(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 4096,
            leaveOpen: true);
    }

    public async Task<ProtocolMessage?> ReadAsync(CancellationToken ct = default)
    {
        while (true)
        {
            var line = await this.reader.ReadLineAsync(ct);
            if (line is null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.Length > MaxLineBytes)
            {
                throw new JsonException("line too long");
            }

            return ProtocolJson.Deserialize(line);
        }
    }

    public async Task WriteAsync(ProtocolMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var body = JsonSerializer.SerializeToUtf8Bytes(message, ProtocolJson.Options);
        var payload = new byte[body.Length + 1];
        Buffer.BlockCopy(body, 0, payload, 0, body.Length);
        payload[^1] = (byte)'\n';

        await this.writeLock.WaitAsync(ct);
        try
        {
            await this.stream.WriteAsync(payload, ct);
            await this.stream.FlushAsync(ct);
        }
        finally
        {
            this.writeLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        this.reader.Dispose();
        this.writeLock.Dispose();

        if (!this.leaveOpen)
        {
            await this.stream.DisposeAsync();
        }
    }
}