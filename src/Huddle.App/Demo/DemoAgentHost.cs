using Microsoft.Extensions.Options;
using System.IO.Pipes;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Demo;

/// <summary>
/// Makes a fresh run self-demonstrating (Team-Specifications.md §6.8): for each configured name, connects an
/// ordinary <see cref="NamedPipeClientStream"/> client to the application's own pipe and echoes back any
/// message that mentions it. It shares no objects with the server and speaks the same envelopes any external
/// pipe client would, so the demo path and the production path are the same code path.
/// </summary>
public sealed class DemoAgentHost : BackgroundService
{
    private const int MaxConnectAttempts = 30;
    private static readonly TimeSpan ConnectRetryDelay = TimeSpan.FromMilliseconds(200);
    private static readonly string[] DefaultNames = ["echo", "alpha"];

    private readonly TeamOptions options;
    private readonly ILogger<DemoAgentHost> logger;

    public DemoAgentHost(IOptions<TeamOptions> options, ILogger<DemoAgentHost> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.options = options.Value;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!this.options.DemoAgent.Enabled)
        {
            return;
        }

        var names = this.options.DemoAgent.Names is { Count: > 0 } configured ? configured : DefaultNames;
        var runs = names.Select(name => this.RunAgentAsync(name, stoppingToken));
        await Task.WhenAll(runs);
    }

    private async Task RunAgentAsync(string name, CancellationToken ct)
    {
        try
        {
            var pipe = await this.ConnectAsync(name, ct);
            if (pipe is null)
            {
                return;
            }

            await using var stream = new JsonLineStream(pipe);
            await stream.WriteAsync(new Hello(name, "Built-in demo agent"), ct);
            if (this.logger.IsEnabled(LogLevel.Information))
            {
                this.logger.LogInformation("Demo agent {AgentName} connected.", name);
            }

            while (true)
            {
                var message = await stream.ReadAsync(ct);
                if (message is null)
                {
                    return;
                }

                if (message is MessagePosted { Mentioned: true } posted)
                {
                    // Strip '@' before quoting so the reply cannot reproduce mentions and re-trigger
                    // another agent (Team-Specifications.md §6.8, "Loop safety").
                    var quoted = posted.Message.Text.Replace("@", string.Empty, StringComparison.Ordinal);
                    var reply = new PostMessage(
                        posted.RoomId,
                        Guid.CreateVersion7().ToString("N"),
                        $"**{name}:** {quoted}");
                    await stream.WriteAsync(reply, ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown: the host is stopping.
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Demo agent {AgentName} stopped unexpectedly.", name);
        }
    }

    private async Task<NamedPipeClientStream?> ConnectAsync(string name, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxConnectAttempts; attempt++)
        {
            var client = new NamedPipeClientStream(".", this.options.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                await client.ConnectAsync(100, ct);
                return client;
            }
            catch (TimeoutException)
            {
                client.Dispose();
            }
            catch (IOException)
            {
                client.Dispose();
            }

            if (attempt < MaxConnectAttempts)
            {
                await Task.Delay(ConnectRetryDelay, ct);
            }
        }

        this.logger.LogWarning(
            "Demo agent {AgentName} failed to connect to pipe {PipeName} after {Attempts} attempts.",
            name, this.options.PipeName, MaxConnectAttempts);
        return null;
    }
}