using System.Globalization;
using System.Text;
using System.Text.Json;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Contracts;

public sealed class JsonLineStreamTests
{
    public static TheoryData<ProtocolMessage> MessageTypes()
    {
        var chatMessage = new ChatMessage(
            "0192f3a1c2007b3e9d3e2c1a5b6d7e91",
            DateTimeOffset.Parse("2026-09-09T10:00:00.1234567+00:00", CultureInfo.InvariantCulture),
            "human",
            "You",
            "hi @echo");
        var member = new MemberInfo("0192f3a1c0f27b3e9d3e2c1a5b6d7e8f", "echo", UserKind.Agent);
        var room = new RoomInfo("0192f3a1c1007b3e9d3e2c1a5b6d7e90", "echo", new List<MemberInfo> { member });

        return new TheoryData<ProtocolMessage>
        {
            new Hello("echo", "PowerShell echo agent"),
            new Welcome(member.Id, "echo", new List<RoomInfo> { room }),
            new PostMessage(room.Id, "reply-1", "**pong**"),
            new MessagePosted(room.Id, "echo", chatMessage, true, new List<MemberInfo> { member }, new List<MemberInfo> { member }, 1, 40),
            new MessageDelta(room.Id, "m-7", "par", false),
            new ToolActivity(room.Id, "m-7", "tc-1", "Reading file.cs", ToolActivityStatus.InProgress),
            new StopTurn(room.Id),
            new ProtocolError(ErrorCodes.NotMember, "Agent 'echo' is not a member of room", "reply-1"),
        };
    }

    [Theory]
    [MemberData(nameof(MessageTypes))]
    public async Task RoundTrip_EachMessageType(ProtocolMessage message)
    {
        var ct = TestContext.Current.CancellationToken;

        using var stream = new MemoryStream();
        var writer = new JsonLineStream(stream, leaveOpen: true);
        await writer.WriteAsync(message, ct);
        await writer.DisposeAsync();

        stream.Position = 0;
        await using var reader = new JsonLineStream(stream, leaveOpen: true);
        var result = await reader.ReadAsync(ct);

        Assert.NotNull(result);
        Assert.Equal(ProtocolVersion.Current, result!.Version);

        // Default record equality does not deep-compare IReadOnlyList<T> properties
        // (List<T> has reference equality), so structural equality is asserted via
        // the canonical JSON representation instead.
        Assert.Equal(ProtocolJson.Serialize(message), ProtocolJson.Serialize(result));
    }

    [Fact]
    public async Task Write_ProducesExactlyOneLine_WithoutRawNewlines()
    {
        var ct = TestContext.Current.CancellationToken;
        var message = new Hello("echo", "line1\r\nline2 \"quoted\"");

        using var stream = new MemoryStream();
        var writer = new JsonLineStream(stream, leaveOpen: true);
        await writer.WriteAsync(message, ct);
        await writer.DisposeAsync();

        var bytes = stream.ToArray();

        Assert.Equal(1, bytes.Count(b => b == (byte)'\n'));
        Assert.DoesNotContain((byte)'\r', bytes);
    }

    [Fact]
    public async Task Read_SkipsBlankLines_AndToleratesCrLf()
    {
        var ct = TestContext.Current.CancellationToken;
        var helloJson = ProtocolJson.Serialize(new Hello("echo", null));
        var postJson = ProtocolJson.Serialize(new PostMessage("room-1", "m1", "hi"));
        var raw = $"\r\n{helloJson}\r\n\r\n{postJson}\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(raw));
        await using var reader = new JsonLineStream(stream, leaveOpen: true);

        var first = await reader.ReadAsync(ct);
        var second = await reader.ReadAsync(ct);
        var third = await reader.ReadAsync(ct);

        Assert.IsType<Hello>(first);
        Assert.IsType<PostMessage>(second);
        Assert.Null(third);
    }

    [Fact]
    public async Task Read_ReturnsNullAtEof()
    {
        var ct = TestContext.Current.CancellationToken;

        using var emptyStream = new MemoryStream();
        await using var emptyReader = new JsonLineStream(emptyStream, leaveOpen: true);
        Assert.Null(await emptyReader.ReadAsync(ct));

        var json = ProtocolJson.Serialize(new Hello("echo", null));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json + "\n"));
        await using var reader = new JsonLineStream(stream, leaveOpen: true);

        Assert.NotNull(await reader.ReadAsync(ct));
        Assert.Null(await reader.ReadAsync(ct));
    }

    [Fact]
    public async Task Read_UnknownType_ThrowsJsonException()
    {
        var ct = TestContext.Current.CancellationToken;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{\"type\":\"nope\"}\n"));
        await using var reader = new JsonLineStream(stream, leaveOpen: true);

        await Assert.ThrowsAsync<JsonException>(() => reader.ReadAsync(ct));
    }

    [Fact]
    public async Task Read_WrongVersion_ThrowsJsonException()
    {
        var ct = TestContext.Current.CancellationToken;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{\"type\":\"hello\",\"version\":99,\"name\":\"x\"}\n"));
        await using var reader = new JsonLineStream(stream, leaveOpen: true);

        await Assert.ThrowsAsync<JsonException>(() => reader.ReadAsync(ct));
    }

    [Fact]
    public async Task Read_LineOverLimit_ThrowsJsonException()
    {
        var ct = TestContext.Current.CancellationToken;
        var oversized = new string('a', JsonLineStream.MaxLineBytes + 1);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(oversized + "\n"));
        await using var reader = new JsonLineStream(stream, leaveOpen: true);

        await Assert.ThrowsAsync<JsonException>(() => reader.ReadAsync(ct));
    }

    [Fact]
    public async Task Write_IsSerialisedUnderConcurrency()
    {
        var ct = TestContext.Current.CancellationToken;

        using var stream = new MemoryStream();
        var writer = new JsonLineStream(stream, leaveOpen: true);
        try
        {
            var tasks = Enumerable.Range(0, 50)
                .Select(i => writer.WriteAsync(new PostMessage("room-1", $"m{i}", $"text {i}"), ct))
                .ToArray();
            await Task.WhenAll(tasks);
        }
        finally
        {
            await writer.DisposeAsync();
        }

        stream.Position = 0;
        await using var reader = new JsonLineStream(stream, leaveOpen: true);

        var count = 0;
        while (await reader.ReadAsync(ct) is not null)
        {
            count++;
        }

        Assert.Equal(50, count);
    }
}