namespace Agency.Huddle.Acp.Tests.Console;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Agency.Huddle.Console.Terminal;
using Xunit;

public sealed class ConsoleLineReaderTests
{
    [Fact(Timeout = 10000)]
    public async Task ReadLineAsync_ReturnsLinesInOrder()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ConsoleLineReader reader = new ConsoleLineReader(new StringReader("a\nb\n"));

        string? first = await reader.ReadLineAsync(cancellationToken);
        string? second = await reader.ReadLineAsync(cancellationToken);

        Assert.Equal("a", first);
        Assert.Equal("b", second);
    }

    [Fact(Timeout = 10000)]
    public async Task ReadLineAsync_Eof_ReturnsNull()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ConsoleLineReader reader = new ConsoleLineReader(new StringReader("a\nb\n"));

        _ = await reader.ReadLineAsync(cancellationToken);
        _ = await reader.ReadLineAsync(cancellationToken);
        string? third = await reader.ReadLineAsync(cancellationToken);

        Assert.Null(third);
    }

    [Fact(Timeout = 10000)]
    public async Task DiscardPending_DropsBufferedLines()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SequenceTextReader sequenceReader = new SequenceTextReader("a", "b");
        using ConsoleLineReader reader = new ConsoleLineReader(sequenceReader);

        bool buffered = SpinWait.SpinUntil(() => sequenceReader.CallCount >= 3, TimeSpan.FromSeconds(2));
        Assert.True(buffered, "Timed out waiting for the background thread to buffer both lines.");

        reader.DiscardPending();

        string? result = await reader.ReadLineAsync(cancellationToken);

        Assert.Null(result);
    }

    [Fact(Timeout = 10000)]
    public async Task ReadLineAsync_CancelledToken_Throws()
    {
        CancellationToken testCancellationToken = TestContext.Current.CancellationToken;
        BlockingTextReader blockingReader = new BlockingTextReader();
        ConsoleLineReader reader = new ConsoleLineReader(blockingReader);
        try
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Task<string?> readTask = reader.ReadLineAsync(cts.Token);
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => readTask).WaitAsync(testCancellationToken);
        }
        finally
        {
            blockingReader.Release();
            reader.Dispose();
        }
    }

    /// <summary>A <see cref="TextReader"/> that replays a fixed sequence of lines and counts how many times <see cref="ReadLine"/> was called.</summary>
    private sealed class SequenceTextReader : TextReader
    {
        private readonly Queue<string?> lines;

        private int callCount;

        internal SequenceTextReader(params string?[] scriptedLines)
        {
            this.lines = new Queue<string?>(scriptedLines);
        }

        internal int CallCount => Volatile.Read(ref this.callCount);

        public override string? ReadLine()
        {
            string? line = this.lines.Count > 0 ? this.lines.Dequeue() : null;
            Interlocked.Increment(ref this.callCount);
            return line;
        }
    }

    /// <summary>
    /// A <see cref="TextReader"/> whose <see cref="ReadLine"/> blocks until <see cref="Release"/>.
    /// </summary>
    /// <remarks>
    /// The gate is a monitor, not a disposable event: <see cref="ConsoleLineReader"/>'s background
    /// thread may reach <see cref="ReadLine"/> only after the test has finished. A
    /// <see cref="ManualResetEventSlim"/> the test disposed then threw
    /// <see cref="ObjectDisposedException"/> on that thread, which nothing catches, and the crash took
    /// down the whole test process.
    /// </remarks>
    private sealed class BlockingTextReader : TextReader
    {
        private readonly object gate = new();

        private bool released;

        /// <summary>Lets every current and future <see cref="ReadLine"/> return end-of-stream.</summary>
        internal void Release()
        {
            lock (this.gate)
            {
                this.released = true;
                Monitor.PulseAll(this.gate);
            }
        }

        public override string? ReadLine()
        {
            lock (this.gate)
            {
                while (!this.released)
                {
                    _ = Monitor.Wait(this.gate);
                }
            }

            return null;
        }
    }
}
