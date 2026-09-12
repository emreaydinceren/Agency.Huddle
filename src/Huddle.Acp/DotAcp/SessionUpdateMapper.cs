namespace Agency.Huddle.Acp.DotAcp;

using System;
using System.Collections.Generic;
using System.Text.Json;
using Agency.Huddle.Acp.Abstractions;

/// <summary>Maps dotacp protocol wire types onto <see cref="Agency.Huddle.Acp.Abstractions"/> types. This is the only place those two worlds meet.</summary>
internal static class SessionUpdateMapper
{
    internal static AgentEvent Map(string sessionId, dotacp.protocol.SessionUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        switch (update)
        {
            case dotacp.protocol.SessionUpdateAgentMessageChunk agentMessageChunk:
            {
                string content = SessionUpdateMapper.ContentText(agentMessageChunk.Content, out bool isText);
                if (isText)
                {
                    return new MessageChunk(sessionId, content);
                }

                return new UnsupportedContent(sessionId, content);
            }

            case dotacp.protocol.SessionUpdateAgentThoughtChunk agentThoughtChunk:
            {
                string content = SessionUpdateMapper.ContentText(agentThoughtChunk.Content, out bool isText);
                if (isText)
                {
                    return new ThoughtChunk(sessionId, content);
                }

                return new UnsupportedContent(sessionId, content);
            }

            case dotacp.protocol.SessionUpdateUserMessageChunk userMessageChunk:
            {
                string content = SessionUpdateMapper.ContentText(userMessageChunk.Content, out bool isText);
                if (isText)
                {
                    return new UserMessageChunk(sessionId, content);
                }

                return new UnsupportedContent(sessionId, content);
            }

            case dotacp.protocol.ToolCall toolCall:
            return new ToolCallStarted(
                sessionId,
                (string)toolCall.ToolCallId,
                toolCall.Title,
                SessionUpdateMapper.MapToolKind(toolCall.Kind),
                SessionUpdateMapper.MapToolCallStatus(toolCall.Status),
                SessionUpdateMapper.SerializeRaw(toolCall.RawInput));

            case dotacp.protocol.SessionUpdateToolCallUpdate toolCallUpdate:
            return new ToolCallUpdated(
                sessionId,
                (string)toolCallUpdate.ToolCallId,
                toolCallUpdate.Title,
                SessionUpdateMapper.MapToolKind(toolCallUpdate.Kind),
                SessionUpdateMapper.MapToolCallStatus(toolCallUpdate.Status),
                SessionUpdateMapper.SerializeRaw(toolCallUpdate.RawOutput));

            case dotacp.protocol.Plan plan:
            return new PlanUpdated(sessionId, SessionUpdateMapper.MapPlanEntries(plan.Entries));

            case dotacp.protocol.UsageUpdate usageUpdate:
            return new UsageUpdated(sessionId, (long)usageUpdate.Size, (long)usageUpdate.Used);

            case dotacp.protocol.CurrentModeUpdate currentModeUpdate:
            return new ModeChanged(sessionId, (string)currentModeUpdate.CurrentModeId);

            default:
            return new UnknownUpdate(sessionId, update.GetType().Name);
        }
    }

    internal static ToolKind MapToolKind(dotacp.protocol.ToolKind kind)
    {
        switch (kind)
        {
            case dotacp.protocol.ToolKind.Read:
            return ToolKind.Read;
            case dotacp.protocol.ToolKind.Edit:
            return ToolKind.Edit;
            case dotacp.protocol.ToolKind.Delete:
            return ToolKind.Delete;
            case dotacp.protocol.ToolKind.Move:
            return ToolKind.Move;
            case dotacp.protocol.ToolKind.Search:
            return ToolKind.Search;
            case dotacp.protocol.ToolKind.Execute:
            return ToolKind.Execute;
            case dotacp.protocol.ToolKind.Think:
            return ToolKind.Think;
            case dotacp.protocol.ToolKind.Fetch:
            return ToolKind.Fetch;
            case dotacp.protocol.ToolKind.SwitchMode:
            return ToolKind.SwitchMode;
            case dotacp.protocol.ToolKind.Other:
            return ToolKind.Other;
            default:
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unmapped dotacp.protocol.ToolKind value.");
        }
    }

    internal static ToolCallStatus MapToolCallStatus(dotacp.protocol.ToolCallStatus status)
    {
        switch (status)
        {
            case dotacp.protocol.ToolCallStatus.Pending:
            return ToolCallStatus.Pending;
            case dotacp.protocol.ToolCallStatus.InProgress:
            return ToolCallStatus.InProgress;
            case dotacp.protocol.ToolCallStatus.Completed:
            return ToolCallStatus.Completed;
            case dotacp.protocol.ToolCallStatus.Failed:
            return ToolCallStatus.Failed;
            default:
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped dotacp.protocol.ToolCallStatus value.");
        }
    }

    internal static StopReason MapStopReason(dotacp.protocol.StopReason reason)
    {
        switch (reason)
        {
            case dotacp.protocol.StopReason.EndTurn:
            return StopReason.EndTurn;
            case dotacp.protocol.StopReason.MaxTokens:
            return StopReason.MaxTokens;
            case dotacp.protocol.StopReason.MaxTurnRequests:
            return StopReason.MaxTurnRequests;
            case dotacp.protocol.StopReason.Refusal:
            return StopReason.Refusal;
            case dotacp.protocol.StopReason.Cancelled:
            return StopReason.Cancelled;
            default:
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unmapped dotacp.protocol.StopReason value.");
        }
    }

    internal static PermissionOptionKind MapPermissionOptionKind(dotacp.protocol.PermissionOptionKind kind)
    {
        switch (kind)
        {
            case dotacp.protocol.PermissionOptionKind.AllowOnce:
            return PermissionOptionKind.AllowOnce;
            case dotacp.protocol.PermissionOptionKind.AllowAlways:
            return PermissionOptionKind.AllowAlways;
            case dotacp.protocol.PermissionOptionKind.RejectOnce:
            return PermissionOptionKind.RejectOnce;
            case dotacp.protocol.PermissionOptionKind.RejectAlways:
            return PermissionOptionKind.RejectAlways;
            default:
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unmapped dotacp.protocol.PermissionOptionKind value.");
        }
    }

    internal static PermissionRequestContext MapPermission(dotacp.protocol.RequestPermissionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        dotacp.protocol.ToolCallUpdate toolCall = request.ToolCall;
        ToolCallInfo toolCallInfo = new ToolCallInfo(
            (string)toolCall.ToolCallId,
            toolCall.Title,
            SessionUpdateMapper.MapToolKind(toolCall.Kind),
            SessionUpdateMapper.MapToolCallStatus(toolCall.Status),
            SessionUpdateMapper.SerializeRaw(toolCall.RawInput));

        List<PermissionOptionInfo> options = new List<PermissionOptionInfo>();
        foreach (dotacp.protocol.PermissionOption option in request.Options)
        {
            options.Add(new PermissionOptionInfo(
                (string)option.OptionId,
                option.Name,
                SessionUpdateMapper.MapPermissionOptionKind(option.Kind)));
        }

        return new PermissionRequestContext((string)request.SessionId, toolCallInfo, options);
    }

    internal static string? SerializeRaw(object? raw)
    {
        if (raw is null)
        {
            return null;
        }

        return JsonSerializer.Serialize(raw);
    }

    internal static string ContentText(dotacp.protocol.ContentBlock block, out bool isText)
    {
        ArgumentNullException.ThrowIfNull(block);

        if (block is dotacp.protocol.TextContent textContent)
        {
            isText = true;
            return textContent.Text;
        }

        isText = false;
        return block.Type;
    }

    private static List<PlanEntryInfo> MapPlanEntries(dotacp.protocol.PlanEntry[] entries)
    {
        List<PlanEntryInfo> mapped = new List<PlanEntryInfo>();
        foreach (dotacp.protocol.PlanEntry entry in entries)
        {
            mapped.Add(new PlanEntryInfo(
                entry.Content,
                SessionUpdateMapper.MapPlanEntryPriority(entry.Priority),
                SessionUpdateMapper.MapPlanEntryStatus(entry.Status)));
        }

        return mapped;
    }

    private static PlanEntryPriority MapPlanEntryPriority(dotacp.protocol.PlanEntryPriority priority)
    {
        switch (priority)
        {
            case dotacp.protocol.PlanEntryPriority.High:
            return PlanEntryPriority.High;
            case dotacp.protocol.PlanEntryPriority.Medium:
            return PlanEntryPriority.Medium;
            case dotacp.protocol.PlanEntryPriority.Low:
            return PlanEntryPriority.Low;
            default:
            throw new ArgumentOutOfRangeException(nameof(priority), priority, "Unmapped dotacp.protocol.PlanEntryPriority value.");
        }
    }

    private static PlanEntryStatus MapPlanEntryStatus(dotacp.protocol.PlanEntryStatus status)
    {
        switch (status)
        {
            case dotacp.protocol.PlanEntryStatus.Pending:
            return PlanEntryStatus.Pending;
            case dotacp.protocol.PlanEntryStatus.InProgress:
            return PlanEntryStatus.InProgress;
            case dotacp.protocol.PlanEntryStatus.Completed:
            return PlanEntryStatus.Completed;
            default:
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped dotacp.protocol.PlanEntryStatus value.");
        }
    }
}