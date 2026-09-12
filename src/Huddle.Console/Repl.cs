namespace Agency.Huddle.Console;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.Tools;
using Agency.Huddle.Console.Terminal;

/// <summary>Drives an interactive read-eval-print loop against an <see cref="IAgentHost"/>.</summary>
internal sealed partial class Repl(
    IAgentHost host,
    IConsoleInput input,
    IConsoleOutput output,
    ConsoleRenderer renderer,
    IPermissionHandler permissionHandler,
    ReplOptions options,
    ILogger<Repl> logger,
    AppToolServer? toolServer = null)
{
    private const string CommandsMessage = "Commands: /exit, /new, /help. Ctrl+C cancels the current turn.";

    private readonly Lock gate = new Lock();

    private CancellationTokenSource? readCancellation;

    private IAgentSession? inFlight;

    internal async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        using CancellationTokenSource linkedReadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (this.gate)
        {
            this.readCancellation = linkedReadCancellation;
        }

        IAgentSession? session = null;
        try
        {
            await host.StartAsync(cancellationToken).ConfigureAwait(false);

            if (options.EnableTools)
            {
                if (toolServer is null)
                {
                    throw new InvalidOperationException("Tools were requested but no tool server was configured.");
                }

                // The tool server must already be listening before session/new carries its URL.
                await toolServer.StartAsync(cancellationToken).ConfigureAwait(false);

                // Printed, not just logged: without it there is no way to tell from the console
                // whether --tools took effect, which makes a silent misconfiguration look like
                // the agent simply choosing not to call the tool.
                output.WriteLine(
                    $"App tools available to the agent at {toolServer.Endpoint.Uri} ({string.Join(", ", toolServer.ToolNames)})",
                    ConsoleStyle.Info);
            }

            AgentHostInfo info = host.Info;
            output.WriteLine($"Connected to {info.AgentName} {info.AgentVersion} (ACP v{info.ProtocolVersion})", ConsoleStyle.Info);
            if (info.AuthMethods.Count > 0)
            {
                string names = string.Join(", ", info.AuthMethods.Select(method => method.Name));
                output.WriteLine("Agent advertises auth methods: " + names, ConsoleStyle.Info);
            }

            try
            {
                session = await this.StartSessionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (AgentAuthenticationRequiredException ex)
            {
                this.WriteAuthRequiredMessage(ex);
                return 2;
            }

            output.WriteLine($"Session {session.SessionId} in {options.Cwd}. {Repl.CommandsMessage}", ConsoleStyle.Info);

            while (true)
            {
                output.Write("> ", ConsoleStyle.Prompt);

                string? line;
                try
                {
                    line = await input.ReadLineAsync(linkedReadCancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    line = null;
                }

                if (line is null || line == "/exit")
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line == "/help")
                {
                    output.WriteLine(Repl.CommandsMessage, ConsoleStyle.Info);
                    continue;
                }

                if (line == "/new")
                {
                    await session.DisposeAsync().ConfigureAwait(false);
                    try
                    {
                        session = await this.StartSessionAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (AgentAuthenticationRequiredException ex)
                    {
                        this.WriteAuthRequiredMessage(ex);
                        return 2;
                    }

                    continue;
                }

                lock (this.gate)
                {
                    this.inFlight = session;
                }

                try
                {
                    Task<PromptResult> promptTask = session.PromptAsync(line, cancellationToken);
                    Task<StopReason?> renderTask = renderer.RenderTurnAsync(session.Events, cancellationToken);
                    await Task.WhenAll(promptTask, renderTask).ConfigureAwait(false);
                    output.WriteLine($"(stop: {promptTask.Result.StopReason})", ConsoleStyle.Info);
                }
                catch (AgentDisconnectedException)
                {
                    output.WriteLine("Agent disconnected.", ConsoleStyle.Error);
                    return 1;
                }
                catch (AgentException ex)
                {
                    output.WriteLine($"Error: {ex.Message}", ConsoleStyle.Error);
                }
                finally
                {
                    lock (this.gate)
                    {
                        this.inFlight = null;
                    }
                }
            }

            return 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Repl.LogUnexpectedFailure(logger, ex);
            output.WriteLine($"Fatal: {ex.Message}", ConsoleStyle.Error);
            return 1;
        }
        finally
        {
            if (session is not null)
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }

            await host.DisposeAsync().ConfigureAwait(false);

            if (options.EnableTools && toolServer is not null)
            {
                await toolServer.DisposeAsync().ConfigureAwait(false);
            }

            lock (this.gate)
            {
                this.readCancellation = null;
            }
        }
    }

    internal void Interrupt()
    {
        IAgentSession? current;
        lock (this.gate)
        {
            current = this.inFlight;
        }

        if (current is not null)
        {
            _ = this.CancelInFlightAsync(current);
            return;
        }

        CancellationTokenSource? cancellation;
        lock (this.gate)
        {
            cancellation = this.readCancellation;
        }

        cancellation?.Cancel();
    }

    private Task<IAgentSession> StartSessionAsync(CancellationToken cancellationToken)
    {
        ToolServerEndpoint? toolServerEndpoint = options.EnableTools ? toolServer?.Endpoint : null;
        return host.StartSessionAsync(
            new AgentSessionOptions(options.Cwd, permissionHandler, options.SystemPrompt, toolServerEndpoint), cancellationToken);
    }

    private void WriteAuthRequiredMessage(AgentAuthenticationRequiredException exception)
    {
        output.WriteLine(
            "Claude Code is not logged in. Run `claude login` (or set ANTHROPIC_API_KEY) and try again.",
            ConsoleStyle.Error);

        if (exception.AuthMethods.Count > 0)
        {
            string names = string.Join(", ", exception.AuthMethods.Select(method => method.Name));
            output.WriteLine("Available auth methods: " + names, ConsoleStyle.Error);
        }
    }

    private async Task CancelInFlightAsync(IAgentSession session)
    {
        try
        {
            await session.CancelAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Repl.LogInterruptFailed(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to cancel the in-flight prompt.")]
    private static partial void LogInterruptFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "The REPL failed unexpectedly.")]
    private static partial void LogUnexpectedFailure(ILogger logger, Exception exception);
}
