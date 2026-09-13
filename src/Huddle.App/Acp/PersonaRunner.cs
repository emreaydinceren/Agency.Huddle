using Microsoft.Extensions.Options;
using System.IO.Pipes;
using System.Text;
using System.Threading.Channels;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Hooks;
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
    private readonly IHookSource hooks;
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

    // The third layer of the cap (roadmap item 2). tokensConsumed is written on the event-reader loop
    // and read on both the read loop and the consumer, so every access goes through Interlocked: a
    // plain long is not guaranteed to be read whole across threads, and warnings-as-errors will not
    // catch that. lastUsed is touched only by the event reader and needs no such care.
    private long tokensConsumed;
    private long lastUsed;

    private TaskCompletionSource<string>? currentTurnTcs;
    private JsonLineStream? stream;
    private IAgentHost? host;
    private IAgentSession? session;
    private Task? readLoopTask;
    private Task? consumerTask;
    private Task? eventReaderTask;
    private bool disposed;

    public PersonaRunner(Persona persona, IOptions<TeamOptions> options, IAgentHostFactory factory, IHookSource hooks, ILogger<PersonaRunner> logger)
    {
        ArgumentNullException.ThrowIfNull(persona);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(hooks);
        ArgumentNullException.ThrowIfNull(logger);

        this.persona = persona;
        this.options = options.Value;
        this.factory = factory;
        this.hooks = hooks;
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
                    // A Human Message ends the unattended run, so the token Budget starts over with
                    // it. The per-Room Budget resets server-side on the very same Message; this one
                    // is per Persona, because one session spans every Room the Agent is in.
                    if (SenderIsHuman(posted))
                    {
                        Interlocked.Exchange(ref this.tokensConsumed, 0);
                    }

                    switch (ReplyGate.Decide(
                        posted.Mentioned, posted.Members.Count, posted.AgentMessagesSinceHuman, posted.Budget))
                    {
                        case ReplyDecision.Reply:
                            var missed = this.TakeCatchUp(posted.RoomId);
                            var item = new WorkItem(posted.RoomId, posted.RoomName, posted.Message.SenderName, posted.Message.Text, missed);

                            // Never call the agent from the read loop: hand the item to the single consumer.
                            this.workItems.Writer.TryWrite(item);
                            break;

                        case ReplyDecision.CatchUp:
                            // Nothing is submitted to the agent until it is Mentioned (the repo owner's
                            // absolute rule), but the Message is not thrown away: it rides along, as
                            // context only, the next time this Agent is Mentioned in that Room.
                            this.AppendCatchUp(posted.RoomId, posted.Message.SenderName, posted.Message.Text);
                            break;

                        case ReplyDecision.BudgetExhausted:
                            // Deliberately NOT buffered as Catch-up. A Catch-up Message was missed; this
                            // one is being held, and if the Human extends the Room's Budget this exact
                            // Message is delivered again - at which point a buffered copy would reach the
                            // model twice in one prompt.
                            this.logger.LogWarning(
                                "Persona '{PersonaName}' declined a turn in room {RoomId}: the room has spent its budget of {Budget} agent messages.",
                                this.persona.Name,
                                posted.RoomId,
                                posted.Budget);
                            break;
                    }
                }
                else if (message is ProtocolError error)
                {
                    // Dropped silently until now, which made a refused post - a spent Budget, a Room
                    // this Agent is not a Member of - invisible outside the server's own log. The Room
                    // cannot be named: this runner sends a null messageId, so RelatedMessageId comes
                    // back null and there is nothing to correlate against.
                    this.logger.LogWarning(
                        "Persona '{PersonaName}' had a message refused: {Code} - {Reason}",
                        this.persona.Name,
                        error.Code,
                        error.Message);
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
        // Checked here rather than in the read loop so that items already queued behind the cap drain
        // without spending anything - the same surprise roadmap item 4 records for cancelling a Turn,
        // where the queue behind it keeps going.
        var tokenBudget = this.options.Acp.TokenBudget;
        if (tokenBudget > 0 && Interlocked.Read(ref this.tokensConsumed) >= tokenBudget)
        {
            this.logger.LogWarning(
                "Persona '{PersonaName}' has spent its token budget of {TokenBudget} and is taking no more turns until a human speaks to it.",
                this.persona.Name,
                tokenBudget);
            return;
        }

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
            var prompt = BuildPrompt(item, this.hooks);
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
                else if (agentEvent is UsageUpdated usage)
                {
                    // Used is how full the context window is, not a running bill - Huddle.Console
                    // renders it as "{Used}/{Size}" - so it FALLS when the session compacts. Summing
                    // Used itself would re-count the whole window on every update; summing only the
                    // rises counts each token once, which is the closest honest proxy for what the
                    // session actually cost.
                    var previous = this.lastUsed;
                    this.lastUsed = usage.Used;
                    if (usage.Used > previous)
                    {
                        Interlocked.Add(ref this.tokensConsumed, usage.Used - previous);
                    }
                }

                // ThoughtChunk, ToolCallStarted, ToolCallUpdated and the rest are ignored.
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

    /// <summary>Builds the prompt text delivered to the model for one Turn.</summary>
    /// <param name="item">The Turn's Room, sender, text and any catch-up context.</param>
    /// <param name="hooks">Resolves each <c>turn.*</c> hook's current text — a configured override, or the <see cref="HookCatalog"/> default.</param>
    /// <returns>
    /// The full prompt: with no catch-up context, the <c>turn.message</c> line alone; with catch-up
    /// context, a <c>turn.catchUpHeader</c> line, one <c>turn.catchUpLine</c> per missed message, a
    /// blank line, then the <c>turn.message</c> line.
    /// </returns>
    internal static string BuildPrompt(WorkItem item, IHookSource hooks)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(hooks);

        // The Room's id rides along with its name because it is the only way an Agent can learn one.
        // mcp__team__post_message and mcp__team__invite_agent both take a room id, and nothing else
        // in a turn carries it: without this they reach only Rooms the Agent created itself.
        var room = RoomLabel(item, hooks);

        if (item.MissedMessages.Count == 0)
        {
            return RenderMessage(hooks, room, item.SenderName, item.Text);
        }

        var builder = new StringBuilder();
        builder.Append(hooks.Render("turn.catchUpHeader", new Dictionary<string, string> { ["{{roomLabel}}"] = room }));
        builder.Append('\n');
        foreach (var missed in item.MissedMessages)
        {
            builder.Append(hooks.Render(
                "turn.catchUpLine",
                new Dictionary<string, string> { ["{{sender}}"] = missed.SenderName, ["{{text}}"] = missed.Text }));
            builder.Append('\n');
        }

        builder.Append('\n');
        builder.Append(RenderMessage(hooks, room, item.SenderName, item.Text));

        return builder.ToString();
    }

    /// <summary>Builds the bracketed Room label that opens every line of a prompt.</summary>
    /// <param name="item">The work item whose Room is being labelled.</param>
    /// <param name="hooks">Resolves the <c>turn.roomLabel</c> hook's current text.</param>
    /// <returns>The label, carrying both the Room's name and its id.</returns>
    private static string RoomLabel(WorkItem item, IHookSource hooks) =>
        hooks.Render(
            "turn.roomLabel",
            new Dictionary<string, string> { ["{{roomName}}"] = item.RoomName, ["{{roomId}}"] = item.RoomId });

    /// <summary>Renders one <c>turn.message</c> line: a Room label, its sender, and its text.</summary>
    /// <param name="hooks">Resolves the <c>turn.message</c> hook's current text.</param>
    /// <param name="roomLabel">The already-rendered Room label to open the line with.</param>
    /// <param name="sender">The message's sender name.</param>
    /// <param name="text">The message's text.</param>
    /// <returns>The rendered <c>turn.message</c> line.</returns>
    private static string RenderMessage(IHookSource hooks, string roomLabel, string sender, string text) =>
        hooks.Render(
            "turn.message",
            new Dictionary<string, string> { ["{{roomLabel}}"] = roomLabel, ["{{sender}}"] = sender, ["{{text}}"] = text });

    /// <summary>
    /// Whether a delivery was authored by the Human. A sender who has left the Room since posting
    /// resolves to no Member at all, and that counts as not-Human on purpose: an unattributable
    /// Message must fail toward the cap rather than reset it.
    /// </summary>
    /// <param name="posted">The delivery to attribute.</param>
    /// <returns><see langword="true"/> only if the sender is still a Member and is the Human.</returns>
    private static bool SenderIsHuman(MessagePosted posted) =>
        posted.Members.FirstOrDefault(m => m.Id == posted.Message.SenderId)?.Kind == UserKind.Human;

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

    internal sealed record WorkItem(
        string RoomId,
        string RoomName,
        string SenderName,
        string Text,
        IReadOnlyList<CaughtUpMessage> MissedMessages);

    internal sealed record CaughtUpMessage(string SenderName, string Text);
}