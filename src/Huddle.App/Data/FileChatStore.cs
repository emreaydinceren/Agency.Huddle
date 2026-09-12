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
        await semaphore.WaitAsync(ct);
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var bytes = JsonSerializer.SerializeToUtf8Bytes(message, ProtocolJson.Options);
            await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
            await stream.WriteAsync(bytes, ct);
            await stream.WriteAsync(NewLine, ct);
            await stream.FlushAsync(ct);
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
        await semaphore.WaitAsync(ct);
        try
        {
            if (!File.Exists(path))
            {
                return [];
            }

            var messages = new List<ChatMessage>();
            await using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync: true))
            {
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var lineNumber = 0;
                while (true)
                {
                    var line = await reader.ReadLineAsync(ct);
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
                        this.logger.LogWarning(ex, "Skipping unparsable line {LineNumber} in room {RoomId}.", lineNumber, roomId);
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

    private string GetRoomPath(string roomId)
    {
        if (!NameRules.IsValidId(roomId))
        {
            throw new ArgumentException($"'{roomId}' is not a valid room id.", nameof(roomId));
        }

        return Path.Combine(this.dataDir, "rooms", $"{roomId}.jsonl");
    }
}