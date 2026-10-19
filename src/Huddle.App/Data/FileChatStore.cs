using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Data;

public sealed class FileChatStore : IChatStore
{
    private static readonly byte[] NewLine = "\n"u8.ToArray();

    private readonly string dataDir;
    private readonly ILogger<FileChatStore> logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> locks = new(StringComparer.Ordinal);

    public FileChatStore(IOptions<TeamOptions> options, ILogger<FileChatStore> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.dataDir = options.Value.DataDir;
        this.logger = logger;
    }

    public async Task AppendAsync(string roomId, ChatMessage message, CancellationToken ct = default)
    {
        var path = this.GetRoomPath(roomId);
        var semaphore = this.locks.GetOrAdd(roomId, static _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var bytes = JsonSerializer.SerializeToUtf8Bytes(message, ProtocolJson.Options);
            var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
            await using var streamScope = stream.ConfigureAwait(false);
            await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
            await stream.WriteAsync(NewLine, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<IReadOnlyList<ChatMessage>> ReadAllAsync(string roomId, CancellationToken ct = default)
    {
        var path = this.GetRoomPath(roomId);
        var semaphore = this.locks.GetOrAdd(roomId, static _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!File.Exists(path))
            {
                return [];
            }

            var messages = new List<ChatMessage>();
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync: true);
            await using (stream.ConfigureAwait(false))
            {
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var lineNumber = 0;
                while (true)
                {
                    var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                    if (line is null)
                    {
                        break;
                    }

                    lineNumber++;
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    try
                    {
                        var message = JsonSerializer.Deserialize<ChatMessage>(line, ProtocolJson.Options);
                        if (message is not null)
                        {
                            messages.Add(message);
                        }
                    }
                    catch (JsonException ex)
                    {
#pragma warning disable HAA0601 // Boxing a value-type log argument is the cost of ILogger params object[]; accepted together with HAA0101.
                        this.logger.LogWarning(ex, "Skipping unparsable line {LineNumber} in room {RoomId}.", lineNumber, roomId);
#pragma warning restore HAA0601
                    }
                }
            }

            return messages;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<IReadOnlyList<ChatMessage>> ReadTailAsync(string roomId, int count, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var path = this.GetRoomPath(roomId);
        var semaphore = this.locks.GetOrAdd(roomId, static _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (count == 0 || !File.Exists(path))
            {
                return [];
            }

            // Read blocks backwards until the tail holds more than `count` line breaks (so its first
            // line is whole) or the start of the file is reached. Bytes are joined before decoding:
            // a block boundary may split a multi-byte character, but never a 0x0A.
            const int BlockSize = 8192;
            List<byte[]> blocks = [];
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync: true);
            await using (stream.ConfigureAwait(false))
            {
                long position = stream.Length;
                var breaks = 0;
                while (position > 0 && breaks <= count)
                {
                    var size = (int)Math.Min(BlockSize, position);
                    position -= size;
                    stream.Seek(position, SeekOrigin.Begin);
                    var block = new byte[size];
                    await stream.ReadExactlyAsync(block, ct).ConfigureAwait(false);
                    blocks.Add(block);
                    breaks += block.Count(static b => b == (byte)'\n');
                }

                blocks.Reverse();
                var tail = Encoding.UTF8.GetString([.. blocks.SelectMany(static b => b)]);
                return this.ParseTailMessages(tail, position > 0, count, roomId);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>Parses the decoded tail of a room file into messages, skipping a possibly partial first line and unparsable lines.</summary>
    /// <param name="tail">The decoded tail text.</param>
    /// <param name="dropFirstLine">True when the tail starts mid-file, so its first line may be partial.</param>
    /// <param name="count">The maximum number of trailing messages to return.</param>
    /// <param name="roomId">The room, for the log message.</param>
    /// <returns>The last <paramref name="count"/> messages of the tail.</returns>
    private List<ChatMessage> ParseTailMessages(string tail, bool dropFirstLine, int count, string roomId)
    {
        string[] lines = tail.Split('\n');
        int firstWhole = dropFirstLine ? 1 : 0;
        List<ChatMessage> messages = [];
        for (int index = firstWhole; index < lines.Length; index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
            {
                continue;
            }

            try
            {
                ChatMessage? message = JsonSerializer.Deserialize<ChatMessage>(lines[index], ProtocolJson.Options);
                if (message is not null)
                {
                    messages.Add(message);
                }
            }
            catch (JsonException ex)
            {
                this.logger.LogWarning(ex, "Skipping unparsable line in the tail of room {RoomId}.", roomId);
            }
        }

        return messages.Count <= count ? messages : [.. messages.Skip(messages.Count - count)];
    }
    public async Task DeleteAsync(string roomId, CancellationToken ct = default)
    {
        var path = this.GetRoomPath(roomId);
        var semaphore = this.locks.GetOrAdd(roomId, static _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        finally
        {
            semaphore.Release();

            // Deliberately left in the dictionary rather than removed: another caller may already be
            // waiting on this exact SemaphoreSlim instance, and removing it here would be a race
            // against that waiter. The entry costs a few bytes, and room ids are Guid.CreateVersion7,
            // never reused, so it can never mismatch a later Room's Transcript.
        }
    }

    public Task<bool> HasMessagesAsync(string roomId, CancellationToken ct = default)
    {
        var path = this.GetRoomPath(roomId);
        var hasMessages = File.Exists(path) && new FileInfo(path).Length > 0;
        return Task.FromResult(hasMessages);
    }

    private string GetRoomPath(string roomId)
    {
        if (!NameRules.IsValidId(roomId))
        {
            throw new ArgumentException($"'{roomId}' is not a valid room id.", nameof(roomId));
        }

        return Path.Combine(this.dataDir, "rooms", $"{roomId}.jsonl");
    }
}