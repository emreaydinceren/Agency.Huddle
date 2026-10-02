using System.Text;
using Agency.Huddle.Acp.DotAcp;
using Xunit;

namespace Agency.Huddle.Acp.Tests.DotAcp;

/// <summary>
/// Covers <see cref="NdjsonMethodRewriteStream"/> (Elicitation E-1): the read-only wrapper that turns
/// an inbound <c>elicitation/create</c> request into <c>_elicitation/create</c> so stable dotacp
/// routes it to <c>ExtMethodAsync</c>, and touches nothing else.
/// </summary>
public sealed class NdjsonMethodRewriteStreamTests
{
    private const string Request = """{"jsonrpc":"2.0","id":7,"method":"elicitation/create","params":{"mode":"form","message":"hi"}}""";

    private const string RewrittenRequest = """{"jsonrpc":"2.0","id":7,"method":"_elicitation/create","params":{"mode":"form","message":"hi"}}""";

    /// <summary>An inbound <c>elicitation/create</c> request comes out with exactly its method changed to <c>_elicitation/create</c>, everything else intact, and its newline kept.</summary>
    [Fact(Timeout = 10000)]
    public async Task Rewrite_ElicitationCreate_BecomesUnderscored()
    {
        string output = await NdjsonMethodRewriteStreamTests.RewriteAsync(Request + "\n", int.MaxValue, TestContext.Current.CancellationToken);

        Assert.Equal(RewrittenRequest + "\n", output);
    }

    /// <summary>A line delivered one byte per read is still rewritten: the rewrite is per line, never per chunk.</summary>
    [Fact(Timeout = 10000)]
    public async Task Rewrite_LineSplitAcrossReads_StillRewritten()
    {
        string output = await NdjsonMethodRewriteStreamTests.RewriteAsync(Request + "\n", 1, TestContext.Current.CancellationToken);

        Assert.Equal(RewrittenRequest + "\n", output);
    }

    /// <summary>A line larger than any internal read buffer is buffered whole and rewritten, with the large payload intact.</summary>
    [Fact(Timeout = 10000)]
    public async Task Rewrite_LineLargerThanReadBuffer_Rewritten()
    {
        string payload = new('x', 200_000);
        string input = Request.Replace("\"hi\"", "\"" + payload + "\"", StringComparison.Ordinal);
        string expected = RewrittenRequest.Replace("\"hi\"", "\"" + payload + "\"", StringComparison.Ordinal);

        string output = await NdjsonMethodRewriteStreamTests.RewriteAsync(input + "\n", 4096, TestContext.Current.CancellationToken);

        Assert.Equal(expected + "\n", output);
    }

    /// <summary>The request is re-serialised with the relaxed encoder: non-ASCII, angle brackets, ampersands and quotes stay literal, not <c>\uXXXX</c>-escaped.</summary>
    [Fact(Timeout = 10000)]
    public async Task Rewrite_NonAsciiAndHtmlCharacters_StayUnescaped()
    {
        const string Input = """{"jsonrpc":"2.0","id":3,"method":"elicitation/create","params":{"message":"Café <b> & 'q'"}}""";
        const string Expected = """{"jsonrpc":"2.0","id":3,"method":"_elicitation/create","params":{"message":"Café <b> & 'q'"}}""";

        string output = await NdjsonMethodRewriteStreamTests.RewriteAsync(Input + "\n", int.MaxValue, TestContext.Current.CancellationToken);

        Assert.Equal(Expected + "\n", output);
    }

    /// <summary>A <c>session/update</c> text chunk that merely contains the method name is passed through byte for byte: the rewrite is never a byte replace.</summary>
    [Fact(Timeout = 10000)]
    public async Task Rewrite_TextChunkContainingTheMethodName_Untouched()
    {
        const string Line = """{"jsonrpc":"2.0","method":"session/update","params":{"update":{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"call elicitation/create now"}}}}""";

        string output = await NdjsonMethodRewriteStreamTests.RewriteAsync(Line + "\n", int.MaxValue, TestContext.Current.CancellationToken);

        Assert.Equal(Line + "\n", output);
    }

    /// <summary>Lines whose method is not exactly <c>elicitation/create</c> are untouched, even ones that contain it: another method, a longer method, a prefixed method, <c>elicitation/complete</c>, and a response carrying the text.</summary>
    [Theory(Timeout = 10000)]
    [InlineData("""{"jsonrpc":"2.0","id":1,"method":"session/request_permission","params":{}}""")]
    [InlineData("""{"jsonrpc":"2.0","id":1,"method":"elicitation/create2","params":{}}""")]
    [InlineData("""{"jsonrpc":"2.0","id":1,"method":"session/elicitation/create","params":{}}""")]
    [InlineData("""{"jsonrpc":"2.0","id":1,"method":"_elicitation/create","params":{}}""")]
    [InlineData("""{"jsonrpc":"2.0","id":1,"method":"elicitation/complete","params":{"note":"elicitation/create"}}""")]
    [InlineData("""{"jsonrpc":"2.0","id":1,"result":{"method":"elicitation/create"}}""")]
    public async Task Rewrite_OtherMethods_Untouched(string line)
    {
        string output = await NdjsonMethodRewriteStreamTests.RewriteAsync(line + "\n", int.MaxValue, TestContext.Current.CancellationToken);

        Assert.Equal(line + "\n", output);
    }

    /// <summary>A notification named <c>elicitation/create</c> (no id) is not a request and is passed through byte for byte, spacing included.</summary>
    [Fact(Timeout = 10000)]
    public async Task Rewrite_NotARequest_NoId_Untouched()
    {
        const string Line = """{ "jsonrpc": "2.0", "method": "elicitation/create", "params": {} }""";

        string output = await NdjsonMethodRewriteStreamTests.RewriteAsync(Line + "\n", int.MaxValue, TestContext.Current.CancellationToken);

        Assert.Equal(Line + "\n", output);
    }

    /// <summary>Text that matches the byte prefilter but is not a JSON object (garbage, or a top-level array) is forwarded unchanged and does not fault the stream.</summary>
    [Theory(Timeout = 10000)]
    [InlineData("not json elicitation/create")]
    [InlineData("""["elicitation/create"]""")]
    [InlineData("""{"method":"elicitation/create","id":1""")]
    public async Task Rewrite_PrefilterHitThatIsNotAnObject_Untouched(string line)
    {
        string output = await NdjsonMethodRewriteStreamTests.RewriteAsync(line + "\n", int.MaxValue, TestContext.Current.CancellationToken);

        Assert.Equal(line + "\n", output);
    }

    /// <summary>A rewritten request keeps a <c>\r\n</c> terminator as it arrived.</summary>
    [Fact(Timeout = 10000)]
    public async Task Rewrite_CrLfTerminator_Preserved()
    {
        string output = await NdjsonMethodRewriteStreamTests.RewriteAsync(Request + "\r\n", 1, TestContext.Current.CancellationToken);

        Assert.Equal(RewrittenRequest + "\r\n", output);
    }

    /// <summary>Several lines in one read are each handled on their own: the request is rewritten, its neighbours and a blank line are not.</summary>
    [Fact(Timeout = 10000)]
    public async Task Rewrite_ManyLinesInOneRead_EachHandledOnItsOwn()
    {
        const string Before = """{"jsonrpc":"2.0","method":"session/update","params":{}}""";
        const string After = """{"jsonrpc":"2.0","id":9,"result":{}}""";
        string input = Before + "\n" + Request + "\n\n" + After + "\n";

        string output = await NdjsonMethodRewriteStreamTests.RewriteAsync(input, int.MaxValue, TestContext.Current.CancellationToken);

        Assert.Equal(Before + "\n" + RewrittenRequest + "\n\n" + After + "\n", output);
    }

    /// <summary>A final request with no trailing newline is flushed at end of stream, rewritten, and no newline is invented.</summary>
    [Fact(Timeout = 10000)]
    public async Task Rewrite_NoTrailingNewline_Flushed()
    {
        string output = await NdjsonMethodRewriteStreamTests.RewriteAsync(Request, 3, TestContext.Current.CancellationToken);

        Assert.Equal(RewrittenRequest, output);
    }

    /// <summary>A final ordinary line with no trailing newline is flushed at end of stream unchanged.</summary>
    [Fact(Timeout = 10000)]
    public async Task Rewrite_NoTrailingNewline_PlainLine_Flushed()
    {
        const string Line = """{"jsonrpc":"2.0","id":1,"result":{}}""";

        string output = await NdjsonMethodRewriteStreamTests.RewriteAsync(Line, 5, TestContext.Current.CancellationToken);

        Assert.Equal(Line, output);
    }

    /// <summary>Every read overload the JSON-RPC layer can use delivers the same rewritten bytes, even through a destination buffer smaller than a line.</summary>
    [Theory(Timeout = 10000)]
    [InlineData("ReadSpan")]
    [InlineData("ReadArray")]
    [InlineData("ReadAsyncMemory")]
    [InlineData("ReadAsyncArray")]
    [InlineData("ReadByte")]
    public async Task Rewrite_EveryReadOverload_DeliversTheRewrittenBytes(string overload)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using NdjsonMethodRewriteStream stream = new(ChunkedReadStream.FromText(Request + "\n", 7));
        using MemoryStream sink = new();
        byte[] array = new byte[5];

        while (true)
        {
            int read;
            switch (overload)
            {
                case "ReadSpan":
                    read = stream.Read(array.AsSpan());
                    break;
                case "ReadArray":
                    read = stream.Read(array, 0, array.Length);
                    break;
                case "ReadAsyncMemory":
                    read = await stream.ReadAsync(array.AsMemory(), cancellationToken);
                    break;
                case "ReadAsyncArray":
#pragma warning disable CA1835 // This case exists to exercise the byte[] overload that the JSON-RPC layer's older paths still call.
                    read = await stream.ReadAsync(array, 0, array.Length, cancellationToken);
#pragma warning restore CA1835
                    break;
                default:
                    int single = stream.ReadByte();
                    read = single < 0 ? 0 : 1;
                    array[0] = (byte)single;
                    break;
            }

            if (read == 0)
            {
                break;
            }

            sink.Write(array, 0, read);
        }

        Assert.Equal(RewrittenRequest + "\n", Encoding.UTF8.GetString(sink.ToArray()));
    }

    /// <summary>A zero-length destination reads nothing, does not touch the stream underneath, and does not consume the data.</summary>
    [Fact(Timeout = 10000)]
    public async Task Rewrite_ZeroLengthRead_ReturnsZeroAndKeepsTheData()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ChunkedReadStream inner = ChunkedReadStream.FromText(Request + "\n", 4);
        using NdjsonMethodRewriteStream stream = new(inner);

        int empty = await stream.ReadAsync(Memory<byte>.Empty, cancellationToken);
        int innerReadsAfterEmpty = inner.ReadCount;
        using MemoryStream sink = new();
        await stream.CopyToAsync(sink, cancellationToken);

        Assert.Equal(0, empty);
        Assert.Equal(0, innerReadsAfterEmpty);
        Assert.Equal(RewrittenRequest + "\n", Encoding.UTF8.GetString(sink.ToArray()));
    }

    /// <summary>An already-cancelled token reaches the inner read: the wrapper does not swallow cancellation.</summary>
    [Fact]
    public async Task Rewrite_ReadAsync_ForwardsTheCancellationToken()
    {
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();
        using NdjsonMethodRewriteStream stream = new(ChunkedReadStream.FromText(Request + "\n", 4));
        byte[] buffer = new byte[16];

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => _ = await stream.ReadAsync(buffer.AsMemory(), cancelled.Token));
    }

    /// <summary>Disposing the wrapper disposes the stream underneath it, which is what lets a host's shutdown release the process pipe.</summary>
    [Fact]
    public void Rewrite_Dispose_ForwardsToTheInnerStream()
    {
        ChunkedReadStream inner = ChunkedReadStream.FromText(Request, 4);
        NdjsonMethodRewriteStream stream = new(inner);

        stream.Dispose();

        Assert.True(inner.Disposed);
    }

    /// <summary>Disposing the wrapper asynchronously disposes the inner stream asynchronously.</summary>
    [Fact]
    public async Task Rewrite_DisposeAsync_ForwardsToTheInnerStream()
    {
        ChunkedReadStream inner = ChunkedReadStream.FromText(Request, 4);
        NdjsonMethodRewriteStream stream = new(inner);

        await stream.DisposeAsync();

        Assert.True(inner.DisposedAsync);
    }

    /// <summary>Reading a disposed wrapper throws <see cref="ObjectDisposedException"/>, synchronously and asynchronously, even while it still holds buffered output (one byte is read first, which buffers the whole line).</summary>
    [Fact(Timeout = 10000)]
    public async Task Rewrite_ReadAfterDispose_ThrowsObjectDisposed()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        NdjsonMethodRewriteStream stream = new(ChunkedReadStream.FromText(Request + "\n", 4));
        int first = stream.ReadByte();
        stream.Dispose();
        Assert.Equal((int)'{', first);
        byte[] buffer = new byte[8];

        Action syncRead = () => _ = stream.Read(buffer, 0, buffer.Length);
        Assert.Throws<ObjectDisposedException>(syncRead);
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => _ = await stream.ReadAsync(buffer.AsMemory(), cancellationToken));
    }

    /// <summary>The wrapper is read-only and not seekable: it reports so, and every write or seek member throws <see cref="NotSupportedException"/>.</summary>
    [Fact]
    public void Rewrite_Capabilities_ReadableNotSeekableNotWritable()
    {
        using NdjsonMethodRewriteStream stream = new(ChunkedReadStream.FromText(Request, 4));

        Assert.True(stream.CanRead);
        Assert.False(stream.CanSeek);
        Assert.False(stream.CanWrite);
        Action write = () => stream.Write([1, 2, 3], 0, 3);
        Action seek = () => stream.Seek(0, SeekOrigin.Begin);
        Action setLength = () => stream.SetLength(0);
        Func<object?> length = () => stream.Length;
        Func<object?> position = () => stream.Position;

        Assert.Throws<NotSupportedException>(write);
        Assert.Throws<NotSupportedException>(seek);
        Assert.Throws<NotSupportedException>(setLength);
        Assert.Throws<NotSupportedException>(length);
        Assert.Throws<NotSupportedException>(position);
    }

    /// <summary>Pushes <paramref name="input"/> through the rewrite stream, served <paramref name="maxChunk"/> bytes per inner read, and returns every byte that comes out.</summary>
    /// <param name="input">The text the "agent" writes.</param>
    /// <param name="maxChunk">The most bytes any inner read returns.</param>
    /// <param name="cancellationToken">The test's token.</param>
    private static async Task<string> RewriteAsync(string input, int maxChunk, CancellationToken cancellationToken)
    {
        using NdjsonMethodRewriteStream stream = new(ChunkedReadStream.FromText(input, maxChunk));
        using MemoryStream sink = new();

        await stream.CopyToAsync(sink, cancellationToken);

        return Encoding.UTF8.GetString(sink.ToArray());
    }
}
