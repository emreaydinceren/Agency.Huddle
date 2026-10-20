using System.Text;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.App.Helpers;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// The production <see cref="IHelperProcessRunner"/>: a fresh adapter process per call, no tool server, a reject-all
/// permission handler, and the Helper's system prompt replacing the adapter's own (Helpers Spec §6.2). No test reaches
/// this class; the manual page covers it.
/// </summary>
/// <param name="options">The bound <see cref="TeamOptions"/>, read for <c>Acp.TraceWire</c>.</param>
/// <param name="loggerFactory">Handed to the launched host and its process launcher.</param>
internal sealed class AdapterProcessHelperRunner(IOptions<TeamOptions> options, ILoggerFactory loggerFactory) : IHelperProcessRunner
{
    /// <summary>How long to wait for the Turn-complete event after the prompt returns, so late chunks are not lost.</summary>
    private static readonly TimeSpan DrainGrace = TimeSpan.FromSeconds(2);

    /// <inheritdoc/>
    public async Task<HelperTurn> RunAsync(AgentProcessOptions processOptions, HelperSessionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processOptions);
        ArgumentNullException.ThrowIfNull(request);

        Directory.CreateDirectory(request.Cwd);
        HelperPermissionHandler permissions = new();
        AgentSessionOptions sessionOptions = new(
            request.Cwd,
            permissions,
            new SystemPromptOptions(request.SystemPrompt, SystemPromptMode.Replace),
            toolServer: null,
            model: request.Model,
            effort: request.Effort,
            meta: request.Meta);

        ThrowawayAdapterSession adapter = await ThrowawayAdapterSession.StartAsync(
            processOptions, sessionOptions, options.Value.Acp.TraceWire, loggerFactory, cancellationToken).ConfigureAwait(false);
        await using var adapterScope = adapter.ConfigureAwait(false);

        IAgentSession session = adapter.Session;
        if (request.Model is not null && session.Models.Count > 0
            && !session.Models.Any(model => string.Equals(model.Id, request.Model, StringComparison.Ordinal)))
        {
            // An unmatched Model would run on the adapter's default, possibly a far dearer model: refuse before prompting.
            return new HelperTurn(string.Empty, ModelOffered: false, permissions.Refused, Cost: null);
        }

        return await PromptAsync(session, permissions, request.Prompt, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sends the prompt and collects the Turn's message text and last reported cost.</summary>
    private static async Task<HelperTurn> PromptAsync(IAgentSession session, HelperPermissionHandler permissions, string prompt, CancellationToken cancellationToken)
    {
        StringBuilder text = new();
        UsageCost? cost = null;
        using CancellationTokenSource stopReading = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task reader = Task.Run(
            async () =>
            {
                await foreach (AgentEvent agentEvent in session.Events.ReadAllAsync(stopReading.Token).ConfigureAwait(false))
                {
                    switch (agentEvent)
                    {
                        case MessageChunk chunk:
                            text.Append(chunk.Text);
                            break;
                        case UsageUpdated { Cost: not null } usage:
                            cost = usage.Cost;
                            break;
                        case TurnCompleted:
                            return;
                    }
                }
            },
            CancellationToken.None);

        _ = await session.PromptAsync(prompt, cancellationToken).ConfigureAwait(false);

        Task finished = await Task.WhenAny(reader, Task.Delay(DrainGrace, cancellationToken)).ConfigureAwait(false);
        if (!ReferenceEquals(finished, reader))
        {
            await stopReading.CancelAsync().ConfigureAwait(false);
        }

        try
        {
            await reader.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The drain grace ended and the reader was stopped on purpose; the text collected so far is the answer.
        }

        return new HelperTurn(text.ToString(), ModelOffered: true, permissions.Refused, cost);
    }
}
