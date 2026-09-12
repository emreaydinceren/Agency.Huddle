namespace Agency.Huddle.Console;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Console.Terminal;

/// <summary>Renders a stream of agent events to the console for the duration of one turn.</summary>
internal sealed class ConsoleRenderer(IConsoleOutput output)
{
    internal async Task<StopReason?> RenderTurnAsync(ChannelReader<AgentEvent> events, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);

        RenderedKind previous = RenderedKind.None;
        Dictionary<string, ToolCallStatus> toolCallStatuses = new Dictionary<string, ToolCallStatus>(StringComparer.Ordinal);

        while (await events.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (events.TryRead(out AgentEvent? agentEvent))
            {
                switch (agentEvent)
                {
                    case MessageChunk messageChunk:
                        output.Write(messageChunk.Text, ConsoleStyle.Agent);
                        previous = RenderedKind.Text;
                        break;

                    case ThoughtChunk thoughtChunk:
                        if (previous != RenderedKind.Thought)
                        {
                            output.WriteLine(string.Empty, ConsoleStyle.Default);
                            output.Write("[thought] ", ConsoleStyle.Thought);
                        }

                        output.Write(thoughtChunk.Text, ConsoleStyle.Thought);
                        previous = RenderedKind.Thought;
                        break;

                    case UserMessageChunk:
                        break;

                    case UnsupportedContent unsupportedContent:
                        output.WriteLine($"[unsupported content: {unsupportedContent.ContentType}]", ConsoleStyle.Info);
                        previous = RenderedKind.Other;
                        break;

                    case ToolCallStarted toolCallStarted:
                        ConsoleRenderer.WriteNewlineIfNeeded(previous, output);
                        string startedKind = toolCallStarted.Kind.ToString().ToLowerInvariant();
                        string startedStatus = toolCallStarted.Status.ToString().ToLowerInvariant();
                        string startedTitle = toolCallStarted.Title ?? toolCallStarted.ToolCallId;
                        output.WriteLine($"[tool:{startedKind}] {startedTitle} ({startedStatus})", ConsoleStyle.Tool);
                        toolCallStatuses[toolCallStarted.ToolCallId] = toolCallStarted.Status;
                        previous = RenderedKind.Other;
                        break;

                    case ToolCallUpdated toolCallUpdated:
                        if (!toolCallStatuses.TryGetValue(toolCallUpdated.ToolCallId, out ToolCallStatus lastStatus)
                            || lastStatus != toolCallUpdated.Status)
                        {
                            string updatedKind = toolCallUpdated.Kind.ToString().ToLowerInvariant();
                            string updatedStatus = toolCallUpdated.Status.ToString().ToLowerInvariant();
                            string updatedTitle = toolCallUpdated.Title ?? toolCallUpdated.ToolCallId;
                            output.WriteLine($"[tool:{updatedKind}] {updatedTitle} -> {updatedStatus}", ConsoleStyle.Tool);
                            toolCallStatuses[toolCallUpdated.ToolCallId] = toolCallUpdated.Status;
                        }

                        previous = RenderedKind.Other;
                        break;

                    case PlanUpdated planUpdated:
                        output.WriteLine("[plan]", ConsoleStyle.Plan);
                        foreach (PlanEntryInfo entry in planUpdated.Entries)
                        {
                            output.WriteLine($"  {ConsoleRenderer.PlanMarker(entry.Status)} {entry.Content}", ConsoleStyle.Plan);
                        }

                        previous = RenderedKind.Other;
                        break;

                    case UsageUpdated usageUpdated:
                        output.WriteLine($"[usage] {usageUpdated.Used}/{usageUpdated.Size}", ConsoleStyle.Usage);
                        previous = RenderedKind.Other;
                        break;

                    case ModeChanged modeChanged:
                        output.WriteLine($"[mode] {modeChanged.ModeId}", ConsoleStyle.Info);
                        previous = RenderedKind.Other;
                        break;

                    case UnknownUpdate unknownUpdate:
                        output.WriteLine($"[update] {unknownUpdate.TypeName}", ConsoleStyle.Info);
                        previous = RenderedKind.Other;
                        break;

                    case TurnCompleted turnCompleted:
                        ConsoleRenderer.WriteNewlineIfNeeded(previous, output);
                        return turnCompleted.StopReason;

                    default:
                        break;
                }
            }
        }

        return null;
    }

    private static string PlanMarker(PlanEntryStatus status)
    {
        switch (status)
        {
            case PlanEntryStatus.Pending:
                return "[ ]";
            case PlanEntryStatus.InProgress:
                return "[~]";
            case PlanEntryStatus.Completed:
                return "[x]";
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown plan entry status.");
        }
    }

    private static void WriteNewlineIfNeeded(RenderedKind previous, IConsoleOutput output)
    {
        if (previous == RenderedKind.Text || previous == RenderedKind.Thought)
        {
            output.WriteLine(string.Empty, ConsoleStyle.Default);
        }
    }

    private enum RenderedKind
    {
        None,
        Text,
        Thought,
        Other,
    }
}
