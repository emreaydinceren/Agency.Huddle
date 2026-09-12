using Microsoft.Extensions.Options;
using System.IO.Pipes;
using System.Text;
using System.Threading.Channels;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Runs one Persona: it brings the Persona online as an Agent and keeps it there. A
/// <see cref="PersonaRunner"/> is simultaneously a pipe client — it dials the application's own named
/// pipe as an ordinary protocol client and registers an Agent, exactly as
/// <see cref="Agency.Huddle.App.Demo.DemoAgentHost"/> does, deliberately with no privileged access to the Team
/// Directory — and an ACP client, owning one <see cref="IAgentHost"/> and one
/// <see cref="IAgentSession"/> with the Persona as its system prompt.
/// </summary>
/// <remarks>
/// Deliberately not a <see cref="Microsoft.Extensions.Hosting.BackgroundService"/>: hosted services are
/// fixed at build time, and instances of this class are created per Persona at runtime.
/// <see cref="PersonaSupervisor"/> owns the lifetime of each instance, one per Persona.
/// </remarks>
internal sealed class PersonaRunner : IAsyncDisposable
{
    private const int MaxConnectAttempts = 30;
    private static readonly TimeSpan ConnectRetryDelay = TimeSpan.FromMilliseconds(200);
    private const int MaxDescriptionLength = 200;

    private readonly Persona persona;
    private readonly TeamOptions options;
    private readonly IAgentHostFactory factory;
    private readonly ILogger<PersonaRunner> logger;
    private readonly CancellationTokenSource runCts = new();
    private readonly Channel<WorkItem> workItems = Channel.CreateUnbounded<WorkItem>();
    private readonly Lock turnLock = new();
    private readonly StringBuilder currentTurnText = new();

    // Per-Room catch-up buffers for Messages the Agent received but was not Mentioned in (ADR-0004).
    // In memory only, per instance, keyed by Room id: a restart loses them, and an Agent that was
    // offline when a Message was delivered never had it to buffer in the first place. Guarded by
    // catchUpLock because, although only the read loop touches these buffers today, nothing about
    // this field's contract promises that will always stay true.
    private readonly Dictionary<string, List<CaughtUpMessage>> catchUpBuffers = [];
    private readonly Lock catchUpLock = new();

    private TaskCompletionSource<string>? currentTurnTcs;
    private JsonLineStream? stream;
    private IAgentHost? host;
    private IAgentSession? session;
    private Task? readLoopTask;
    private Task? consumerTask;
    private Task? eventReaderTask;
    private bool disposed;

    public PersonaRunner(Persona persona, IOptions<TeamOptions> options, IAgentHostFactory factory, ILogger<PersonaRunner> logger)
    {
        ArgumentNullException.ThrowIfNull(persona);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(logger);

        this.persona = persona;
        this.options = options.Value;
        this.factory = factory;
        this.logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var client = await this.ConnectAsync(cancellationToken);
        var jsonLineStream = new JsonLineStream(client);
        this.stream = jsonLineStream;

        var description = BuildDescription(this.persona.Text);
        await jsonLineStream.WriteAsync(new Hello(this.persona.Name, description), cancellationToken);

        var welcomeMessage = await jsonLineStream.ReadAsync(cancellationToken);
        if (welcomeMessage is not Welcome welcome)
        {
            throw new InvalidOperationException(
                $"Expected a Welcome envelope for Persona '{this.persona.Name}' but received "
                + (welcomeMessage is null ? "end of stream." : $"{welcomeMessage.GetType().Name}."));
        }

        // Only now, with Registration complete, does the Agent id exist, so only now can the session
        // be created with the tools bound to it by construction (docs/acp/agent-guide.md §3.6).
        var created = await this.factory.CreateAsync(this.persona, welcome.AgentId, cancellationToken);
        this.host = created.Host;
        this.session = created.Session;

        this.eventReaderTask = this.RunEventReaderAsync(this.runCts.Token);
        this.consumerTask = this.RunConsumerAsync(this.runCts.Token);
        this.readLoopTask = this.RunReadLoopAsync(this.runCts.Token);
    }

    public async Task StopAsync()
    {
        if (this.runCts.IsCancellationRequested)
        {
            return;
        }

        await this.runCts.CancelAsync();
        this.workItems.Writer.TryComplete();

        await SafeAwaitAsync(this.readLoopTask);
        await SafeAwaitAsync(this.consumerTask);
        await SafeAwaitAsync(this.eventReaderTask);

        if (this.session is not null)
        {
            await this.session.DisposeAsync();
        }

        if (this.host is not null)
        {
            await this.host.DisposeAsync();
        }

        if (this.stream is not null)
        {
            await this.stream.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        await this.StopAsync();
        this.runCts.Dispose();
    }

    private async Task RunReadLoopAsync(CancellationToken ct)
    {
        try
        {
            while (true)
            {
                var message = await this.stream!.ReadAsync(ct);
                if (message is null)
                {
                    return;
                }

                if (message is MessagePosted posted)
                {
                    if (ReplyGate.ShouldReply(posted.Mentioned, posted.Members.Count))
                    {
                        var missed = this.TakeCatchUp(posted.RoomId);
                        var item = new WorkItem(posted.RoomId, posted.RoomName, posted.Message.SenderName, posted.Message.Text, missed);

                        // Never call the agent from the read loop: hand the item to the single consumer.
                        this.workItems.Writer.TryWrite(item);
                    }
                    else
                    {
                        // Nothing is submitted to the agent until it is Mentioned (the repo owner's
                        // absolute rule), but the Message is not thrown away: it rides along, as
                        // context only, the next time this Agent is Mentioned in that Room.
                        this.AppendCatchUp(posted.RoomId, posted.Message.SenderName, posted.Message.Text);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Persona '{PersonaName}' read loop stopped unexpectedly.", this.persona.Name);
        }
    }

    private async Task RunConsumerAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var item in this.workItems.Reader.ReadAllAsync(ct))
            {
                await this.ProcessWorkItemAsync(item, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    private async Task ProcessWorkItemAsync(WorkItem item, CancellationToken ct)
    {
        // Exactly one session serves every Room the Agent is in (there is one session per Persona),
        // so a failed turn must log and continue rather than end the loop: a dying agent process must
        // not silently deafen the Agent for every other Room.
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (this.turnLock)
        {
            this.currentTurnText.Clear();
            this.currentTurnTcs = tcs;
        }

        try
        {
            var prompt = BuildPrompt(item);
            await this.session!.PromptAsync(prompt, ct);
            var replyText = await tcs.Task.WaitAsync(ct);

            if (!string.IsNullOrWhiteSpace(replyText))
            {
                await this.stream!.WriteAsync(new PostMessage(item.RoomId, null, replyText), ct);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Persona '{PersonaName}' failed to process a turn in room {RoomId}.", this.persona.Name, item.RoomId);
        }
    }

    private async Task RunEventReaderAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var agentEvent in this.session!.Events.ReadAllAsync(ct))
            {
                if (agentEvent is MessageChunk chunk)
                {
                    lock (this.turnLock)
                    {
                        this.currentTurnText.Append(chunk.Text);
                    }
                }
                else if (agentEvent is TurnCompleted)
                {
                    TaskCompletionSource<string>? completedTcs;
                    string text;
                    lock (this.turnLock)
                    {
                        completedTcs = this.currentTurnTcs;
                        text = this.currentTurnText.ToString();
                        this.currentTurnText.Clear();
                        this.currentTurnTcs = null;
                    }

                    completedTcs?.TrySetResult(text);
                }

                // ThoughtChunk, ToolCallStarted, ToolCallUpdated, UsageUpdated and the rest are ignored.
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    private async Task<NamedPipeClientStream> ConnectAsync(CancellationToken ct)
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

        throw new InvalidOperationException(
            $"Persona '{this.persona.Name}' failed to connect to pipe '{this.options.PipeName}' after {MaxConnectAttempts} attempts.");
    }

    private static string BuildPrompt(WorkItem item)
    {
        // The Room's id rides along with its name because it is the only way an Agent can learn one.
        // mcp__team__post_message and mcp__team__invite_agent both take a room id, and nothing else
        // in a turn carries it: without this they reach only Rooms the Agent created itself.
        var room = RoomLabel(item);

        if (item.MissedMessages.Count == 0)
        {
            return $"{room} {item.SenderName}: {item.Text}";
        }

        var builder = new StringBuilder();
        builder.Append(room).Append(" You were not addressed in these earlier messages, they are context only:");
        builder.Append('\n');
        foreach (var missed in item.MissedMessages)
        {
            builder.Append(missed.SenderName).Append(": ").Append(missed.Text);
            builder.Append('\n');
        }

        builder.Append('\n');
        builder.Append(room).Append(' ').Append(item.SenderName).Append(": ").Append(item.Text);

        return builder.ToString();
    }

    /// <summary>Builds the bracketed Room label that opens every line of a prompt.</summary>
    /// <param name="item">The work item whose Room is being labelled.</param>
    /// <returns>The label, carrying both the Room's name and its id.</returns>
    private static string RoomLabel(WorkItem item) => $"[Room: {item.RoomName} (id: {item.RoomId})]";

    private void AppendCatchUp(string roomId, string senderName, string text)
    {
        lock (this.catchUpLock)
        {
            if (!this.catchUpBuffers.TryGetValue(roomId, out var buffer))
            {
                buffer = [];
                this.catchUpBuffers[roomId] = buffer;
            }

            buffer.Add(new CaughtUpMessage(senderName, text));

            var max = this.options.Acp.CatchUpMessages;
            while (buffer.Count > max)
            {
                buffer.RemoveAt(0);
            }
        }
    }

    private CaughtUpMessage[] TakeCatchUp(string roomId)
    {
        lock (this.catchUpLock)
        {
            if (!this.catchUpBuffers.TryGetValue(roomId, out var buffer) || buffer.Count == 0)
            {
                return [];
            }

            var taken = buffer.ToArray();
            buffer.Clear();
            return taken;
        }
    }

    private static string BuildDescription(string personaText)
    {
        // The body, not the raw text: personaText may open with a YAML frontmatter block, whose
        // first line is always "---", not something fit to use as a description.
        var (_, body) = PersonaFrontmatter.Parse(personaText);

        foreach (var rawLine in body.Split('\n'))
        {
            var line = rawLine.Trim().TrimStart('#').Trim();
            if (line.Length == 0)
            {
                continue;
            }

            return line.Length > MaxDescriptionLength ? line[..MaxDescriptionLength] : line;
        }

        return string.Empty;
    }

    private static async Task SafeAwaitAsync(Task? task)
    {
        if (task is null)
        {
            return;
        }

        try
        {
            await task;
        }
        catch (Exception)
        {
            // Teardown must not throw regardless of how the task ended.
        }
    }

    private sealed record WorkItem(
        string RoomId,
        string RoomName,
        string SenderName,
        string Text,
        IReadOnlyList<CaughtUpMessage> MissedMessages);

    private sealed record CaughtUpMessage(string SenderName, string Text);
}