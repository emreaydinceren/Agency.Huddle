namespace Agency.Huddle.Console.Terminal;

using System;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

/// <summary>
/// Reads lines from a <see cref="TextReader"/> on a dedicated background thread and republishes
/// them through a channel, so that <see cref="ReadLineAsync"/> can be genuinely cancelled even
/// though the underlying <see cref="TextReader.ReadLine"/> call cannot be interrupted.
/// </summary>
internal sealed class ConsoleLineReader : IConsoleInput, IDisposable
{
    private readonly TextReader reader;

    private readonly Channel<string> channel = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = false, SingleWriter = true });

    private readonly Thread thread;

    private bool disposed;

    internal ConsoleLineReader(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        this.reader = reader;
        this.thread = new Thread(this.ReadLoop)
        {
            IsBackground = true,
            Name = "ConsoleLineReader",
        };
        this.thread.Start();
    }

    public async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await this.channel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            return null;
        }
    }

    public void DiscardPending()
    {
        while (this.channel.Reader.TryRead(out _))
        {
        }
    }

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        this.channel.Writer.TryComplete();
    }

    private void ReadLoop()
    {
        try
        {
            while (true)
            {
                string? line = this.reader.ReadLine();
                if (line is null)
                {
                    break;
                }

                this.channel.Writer.TryWrite(line);
            }
        }
        finally
        {
            this.channel.Writer.TryComplete();
        }
    }
}
