namespace Agency.Huddle.Acp.Tests.DotAcp;

using System;
using System.Collections.Generic;
using System.Text.Json;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Xunit;

public sealed class SessionUpdateMapperTests
{
    [Fact]
    public void AgentMessageChunk_Text_MapsToMessageChunk()
    {
        dotacp.protocol.SessionUpdateAgentMessageChunk update = new dotacp.protocol.SessionUpdateAgentMessageChunk
        {
            Content = new dotacp.protocol.TextContent { Text = "hi" },
        };

        AgentEvent result = SessionUpdateMapper.Map("s", update);

        Assert.Equal(new MessageChunk("s", "hi"), result);
    }

    [Fact]
    public void AgentMessageChunk_Image_MapsToUnsupportedContent()
    {
        dotacp.protocol.SessionUpdateAgentMessageChunk update = new dotacp.protocol.SessionUpdateAgentMessageChunk
        {
            Content = new dotacp.protocol.ImageContent { Data = "abc", MimeType = "image/png" },
        };

        AgentEvent result = SessionUpdateMapper.Map("s", update);

        UnsupportedContent unsupported = Assert.IsType<UnsupportedContent>(result);
        Assert.Equal("image", unsupported.ContentType);
    }

    [Fact]
    public void AgentThoughtChunk_MapsToThoughtChunk()
    {
        dotacp.protocol.SessionUpdateAgentThoughtChunk update = new dotacp.protocol.SessionUpdateAgentThoughtChunk
        {
            Content = new dotacp.protocol.TextContent { Text = "thinking" },
        };

        AgentEvent result = SessionUpdateMapper.Map("s", update);

        Assert.Equal(new ThoughtChunk("s", "thinking"), result);
    }

    [Fact]
    public void UserMessageChunk_MapsToUserMessageChunk()
    {
        dotacp.protocol.SessionUpdateUserMessageChunk update = new dotacp.protocol.SessionUpdateUserMessageChunk
        {
            Content = new dotacp.protocol.TextContent { Text = "echoed" },
        };

        AgentEvent result = SessionUpdateMapper.Map("s", update);

        Assert.Equal(new UserMessageChunk("s", "echoed"), result);
    }

    [Fact]
    public void ToolCall_MapsIdTitleKindStatusAndRawInputJson()
    {
        JsonElement rawInput = JsonSerializer.Deserialize<JsonElement>("{\"path\":\"x\"}");
        dotacp.protocol.ToolCall update = new dotacp.protocol.ToolCall
        {
            ToolCallId = "call-1",
            Title = "Read x",
            Kind = dotacp.protocol.ToolKind.Read,
            Status = dotacp.protocol.ToolCallStatus.Pending,
            RawInput = rawInput,
        };

        AgentEvent result = SessionUpdateMapper.Map("s", update);

        Assert.Equal(
            new ToolCallStarted("s", "call-1", "Read x", ToolKind.Read, ToolCallStatus.Pending, "{\"path\":\"x\"}"),
            result);
    }

    [Fact]
    public void ToolCallUpdate_MapsFields()
    {
        JsonElement rawOutput = JsonSerializer.Deserialize<JsonElement>("{\"result\":\"ok\"}");
        dotacp.protocol.SessionUpdateToolCallUpdate update = new dotacp.protocol.SessionUpdateToolCallUpdate
        {
            ToolCallId = "call-1",
            Title = "Read x",
            Kind = dotacp.protocol.ToolKind.Read,
            Status = dotacp.protocol.ToolCallStatus.Completed,
            RawOutput = rawOutput,
        };

        AgentEvent result = SessionUpdateMapper.Map("s", update);

        Assert.Equal(
            new ToolCallUpdated("s", "call-1", "Read x", ToolKind.Read, ToolCallStatus.Completed, "{\"result\":\"ok\"}"),
            result);
    }

    /// <summary>
    /// Finding P-1: <c>claude-agent-acp</c> sends a streamed tool call's complete input on a later
    /// <c>tool_call_update</c>, not on the initial <c>tool_call</c>. <see cref="ToolCallUpdated.RawInputJson"/>
    /// must carry it through, alongside the existing <see cref="ToolCallUpdated.RawOutputJson"/> (asserted via <see cref="ToolCallUpdate_MapsFields"/>).
    /// </summary>
    [Fact]
    public void Map_ToolCallUpdateWithRawInput_CarriesRawInputJson()
    {
        JsonElement rawInput = JsonSerializer.Deserialize<JsonElement>("{\"file_path\":\"C:\\\\x\\\\a.md\"}");
        dotacp.protocol.SessionUpdateToolCallUpdate update = new dotacp.protocol.SessionUpdateToolCallUpdate
        {
            ToolCallId = "call-1",
            Title = "Edit a.md",
            Kind = dotacp.protocol.ToolKind.Edit,
            Status = dotacp.protocol.ToolCallStatus.Completed,
            RawInput = rawInput,
        };

        AgentEvent result = SessionUpdateMapper.Map("s", update);

        ToolCallUpdated updated = Assert.IsType<ToolCallUpdated>(result);
        Assert.Equal(ToolKind.Edit, updated.Kind);
        Assert.NotNull(updated.RawInputJson);
        Assert.Contains("a.md", updated.RawInputJson, StringComparison.Ordinal);
    }

    /// <summary>A <see cref="dotacp.protocol.SessionUpdateToolCallUpdate"/> carrying no <c>RawInput</c> maps to a <see langword="null"/> <see cref="ToolCallUpdated.RawInputJson"/>.</summary>
    [Fact]
    public void Map_ToolCallUpdateWithoutRawInput_RawInputJsonIsNull()
    {
        dotacp.protocol.SessionUpdateToolCallUpdate update = new dotacp.protocol.SessionUpdateToolCallUpdate
        {
            ToolCallId = "call-1",
            Title = "Read x",
            Kind = dotacp.protocol.ToolKind.Read,
            Status = dotacp.protocol.ToolCallStatus.Completed,
        };

        AgentEvent result = SessionUpdateMapper.Map("s", update);

        ToolCallUpdated updated = Assert.IsType<ToolCallUpdated>(result);
        Assert.Null(updated.RawInputJson);
    }

    [Fact]
    public void Plan_MapsEntries()
    {
        dotacp.protocol.Plan update = new dotacp.protocol.Plan
        {
            Entries = new dotacp.protocol.PlanEntry[]
            {
                new dotacp.protocol.PlanEntry
                {
                    Content = "step one",
                    Priority = dotacp.protocol.PlanEntryPriority.High,
                    Status = dotacp.protocol.PlanEntryStatus.Pending,
                },
                new dotacp.protocol.PlanEntry
                {
                    Content = "step two",
                    Priority = dotacp.protocol.PlanEntryPriority.Low,
                    Status = dotacp.protocol.PlanEntryStatus.Completed,
                },
            },
        };

        AgentEvent result = SessionUpdateMapper.Map("s", update);

        PlanUpdated planUpdated = Assert.IsType<PlanUpdated>(result);
        Assert.Equal("s", planUpdated.SessionId);
        Assert.Equal(
            new List<PlanEntryInfo>
            {
                new PlanEntryInfo("step one", PlanEntryPriority.High, PlanEntryStatus.Pending),
                new PlanEntryInfo("step two", PlanEntryPriority.Low, PlanEntryStatus.Completed),
            },
            planUpdated.Entries);
    }

    [Fact]
    public void UsageUpdate_MapsNumbers()
    {
        dotacp.protocol.UsageUpdate update = new dotacp.protocol.UsageUpdate { Size = 1000, Used = 250 };

        AgentEvent result = SessionUpdateMapper.Map("s", update);

        Assert.Equal(new UsageUpdated("s", 1000, 250), result);
    }

    /// <summary>A tool call's first <c>diff</c> block and first location reach the event, so the Room view can name the file and show the change.</summary>
    [Fact]
    public void Map_ToolCallWithDiffAndLocation_CarriesBoth()
    {
        dotacp.protocol.ToolCall update = new dotacp.protocol.ToolCall
        {
            ToolCallId = "call-1",
            Title = "Edit notes.md",
            Kind = dotacp.protocol.ToolKind.Edit,
            Status = dotacp.protocol.ToolCallStatus.InProgress,
            Content = new dotacp.protocol.ToolCallContent[]
            {
                new dotacp.protocol.Diff { Path = "E:\\work\\notes.md", OldText = "one", NewText = "two" },
            },
            Locations = new[] { new dotacp.protocol.ToolCallLocation { Path = "E:\\work\\notes.md", Line = 12 } },
        };

        ToolCallStarted result = Assert.IsType<ToolCallStarted>(SessionUpdateMapper.Map("s", update));

        Assert.Equal(new ToolCallDiffInfo("E:\\work\\notes.md", "one", "two"), result.Diff);
        Assert.Equal(new ToolCallLocationInfo("E:\\work\\notes.md", 12), result.Location);
    }

    /// <summary>An update that omits <c>content</c> and <c>locations</c> maps to null, which the consumer reads as "unchanged".</summary>
    [Fact]
    public void Map_ToolCallUpdateWithoutContent_LeavesDiffAndLocationNull()
    {
        dotacp.protocol.SessionUpdateToolCallUpdate update = new dotacp.protocol.SessionUpdateToolCallUpdate
        {
            ToolCallId = "call-1",
            Kind = dotacp.protocol.ToolKind.Edit,
            Status = dotacp.protocol.ToolCallStatus.Completed,
        };

        ToolCallUpdated result = Assert.IsType<ToolCallUpdated>(SessionUpdateMapper.Map("s", update));

        Assert.Null(result.Diff);
        Assert.Null(result.Location);
    }

    /// <summary>An update carries the diff and the line when the Adapter sends them on a later notification.</summary>
    [Fact]
    public void Map_ToolCallUpdateWithDiffAndLine_CarriesBoth()
    {
        dotacp.protocol.SessionUpdateToolCallUpdate update = new dotacp.protocol.SessionUpdateToolCallUpdate
        {
            ToolCallId = "call-1",
            Kind = dotacp.protocol.ToolKind.Edit,
            Status = dotacp.protocol.ToolCallStatus.InProgress,
            Content = new dotacp.protocol.ToolCallContent[]
            {
                new dotacp.protocol.Diff { Path = "E:\\work\\notes.md", OldText = "one", NewText = "two" },
            },
            Locations = new[] { new dotacp.protocol.ToolCallLocation { Path = "E:\\work\\notes.md", Line = 12 } },
        };

        ToolCallUpdated result = Assert.IsType<ToolCallUpdated>(SessionUpdateMapper.Map("s", update));

        Assert.Equal(new ToolCallDiffInfo("E:\\work\\notes.md", "one", "two"), result.Diff);
        Assert.Equal(new ToolCallLocationInfo("E:\\work\\notes.md", 12), result.Location);
    }

    /// <summary>A write creates a file, so the diff has no old side.</summary>
    [Fact]
    public void Map_WriteDiff_HasNullOldText()
    {
        dotacp.protocol.ToolCall update = new dotacp.protocol.ToolCall
        {
            ToolCallId = "call-1",
            Kind = dotacp.protocol.ToolKind.Edit,
            Status = dotacp.protocol.ToolCallStatus.InProgress,
            Content = new dotacp.protocol.ToolCallContent[]
            {
                new dotacp.protocol.Diff { Path = "E:\\work\\new.txt", NewText = "hello" },
            },
        };

        ToolCallStarted result = Assert.IsType<ToolCallStarted>(SessionUpdateMapper.Map("s", update));

        Assert.Equal(new ToolCallDiffInfo("E:\\work\\new.txt", null, "hello"), result.Diff);
    }

    /// <summary>A multi-change call keeps its first change and counts the others, so the view can say it shows one of several.</summary>
    [Fact]
    public void Map_TwoDiffBlocks_KeepsTheFirstAndCountsTheRest()
    {
        dotacp.protocol.ToolCall update = new dotacp.protocol.ToolCall
        {
            ToolCallId = "call-1",
            Kind = dotacp.protocol.ToolKind.Edit,
            Status = dotacp.protocol.ToolCallStatus.InProgress,
            Content = new dotacp.protocol.ToolCallContent[]
            {
                new dotacp.protocol.Diff { Path = "a.txt", OldText = "1", NewText = "2" },
                new dotacp.protocol.Diff { Path = "a.txt", OldText = "3", NewText = "4" },
            },
        };

        ToolCallStarted result = Assert.IsType<ToolCallStarted>(SessionUpdateMapper.Map("s", update));

        Assert.Equal(new ToolCallDiffInfo("a.txt", "1", "2", 1), result.Diff);
    }

    /// <summary>A diff block whose <c>newText</c> never arrived is a change to empty text, not a failure.</summary>
    [Fact]
    public void Map_DiffWithoutNewText_HasEmptyNewText()
    {
        dotacp.protocol.ToolCall update = new dotacp.protocol.ToolCall
        {
            ToolCallId = "call-1",
            Kind = dotacp.protocol.ToolKind.Edit,
            Status = dotacp.protocol.ToolCallStatus.InProgress,
            Content = new dotacp.protocol.ToolCallContent[]
            {
                new dotacp.protocol.Diff { Path = "a.txt", OldText = "x" },
            },
        };

        ToolCallStarted result = Assert.IsType<ToolCallStarted>(SessionUpdateMapper.Map("s", update));

        Assert.Equal(new ToolCallDiffInfo("a.txt", "x", string.Empty), result.Diff);
    }

    /// <summary>A non-diff content block (for example terminal output) is not a diff.</summary>
    [Fact]
    public void Map_ContentWithoutADiffBlock_LeavesDiffNull()
    {
        dotacp.protocol.ToolCall update = new dotacp.protocol.ToolCall
        {
            ToolCallId = "call-1",
            Kind = dotacp.protocol.ToolKind.Read,
            Status = dotacp.protocol.ToolCallStatus.InProgress,
            Content = new dotacp.protocol.ToolCallContent[]
            {
                new dotacp.protocol.Terminal { TerminalId = "term-1" },
            },
        };

        ToolCallStarted result = Assert.IsType<ToolCallStarted>(SessionUpdateMapper.Map("s", update));

        Assert.Null(result.Diff);
    }

    /// <summary>A usage update with a cost carries the amount and the currency.</summary>
    [Fact]
    public void Map_UsageUpdateWithCost_CarriesAmountAndCurrency()
    {
        dotacp.protocol.UsageUpdate update = new dotacp.protocol.UsageUpdate
        {
            Size = 1000,
            Used = 250,
            Cost = new dotacp.protocol.Cost { Amount = 0.054828, Currency = "USD" },
        };

        UsageUpdated result = Assert.IsType<UsageUpdated>(SessionUpdateMapper.Map("s", update));

        Assert.Equal(new UsageCost(0.054828m, "USD"), result.Cost);
    }

    /// <summary>A usage update without a cost leaves it null, so a local model shows no Spend.</summary>
    [Fact]
    public void Map_UsageUpdateWithoutCost_LeavesCostNull()
    {
        dotacp.protocol.UsageUpdate update = new dotacp.protocol.UsageUpdate { Size = 1000, Used = 250 };

        UsageUpdated result = Assert.IsType<UsageUpdated>(SessionUpdateMapper.Map("s", update));

        Assert.Null(result.Cost);
    }

    [Fact]
    public void CurrentModeUpdate_MapsModeChanged()
    {
        dotacp.protocol.CurrentModeUpdate update = new dotacp.protocol.CurrentModeUpdate { CurrentModeId = "plan" };

        AgentEvent result = SessionUpdateMapper.Map("s", update);

        Assert.Equal(new ModeChanged("s", "plan"), result);
    }

    /// <summary>An advertised command list maps to an event carrying each command's name, description and input hint.</summary>
    [Fact]
    public void AvailableCommandsUpdate_MapsNamesDescriptionsAndHints()
    {
        dotacp.protocol.AvailableCommandsUpdate update = new dotacp.protocol.AvailableCommandsUpdate
        {
            AvailableCommands =
            [
                new dotacp.protocol.AvailableCommand
                {
                    Name = "compact",
                    Description = "Free up context by summarizing the conversation so far",
                    Input = new dotacp.protocol.UnstructuredCommandInput { Hint = "<optional custom summarization instructions>" },
                },
                new dotacp.protocol.AvailableCommand { Name = "init", Description = "Initialize a CLAUDE.md file" },
            ],
        };

        AvailableCommandsUpdated result = Assert.IsType<AvailableCommandsUpdated>(SessionUpdateMapper.Map("s", update));

        Assert.Equal("s", result.SessionId);
        Assert.Equal(
            [
                new AvailableCommandInfo("compact", "Free up context by summarizing the conversation so far", "<optional custom summarization instructions>"),
                new AvailableCommandInfo("init", "Initialize a CLAUDE.md file", null),
            ],
            result.Commands);
    }

    /// <summary>An empty advertised list maps to an empty event, not to <see cref="UnknownUpdate"/>.</summary>
    [Fact]
    public void AvailableCommandsUpdate_EmptyList_MapsToEmptyEvent()
    {
        dotacp.protocol.AvailableCommandsUpdate update = new dotacp.protocol.AvailableCommandsUpdate { AvailableCommands = [] };

        AvailableCommandsUpdated result = Assert.IsType<AvailableCommandsUpdated>(SessionUpdateMapper.Map("s", update));

        Assert.Empty(result.Commands);
    }

    public static TheoryData<dotacp.protocol.ToolKind> ToolKindValues()
    {
        TheoryData<dotacp.protocol.ToolKind> data = new TheoryData<dotacp.protocol.ToolKind>();
        foreach (dotacp.protocol.ToolKind value in Enum.GetValues<dotacp.protocol.ToolKind>())
        {
            data.Add(value);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ToolKindValues))]
    public void MapToolKind_CoversEveryDotAcpValue(dotacp.protocol.ToolKind value)
    {
        ToolKind mapped = SessionUpdateMapper.MapToolKind(value);

        Assert.Equal(Enum.GetName(value), Enum.GetName(mapped));
    }

    public static TheoryData<dotacp.protocol.ToolCallStatus> ToolCallStatusValues()
    {
        TheoryData<dotacp.protocol.ToolCallStatus> data = new TheoryData<dotacp.protocol.ToolCallStatus>();
        foreach (dotacp.protocol.ToolCallStatus value in Enum.GetValues<dotacp.protocol.ToolCallStatus>())
        {
            data.Add(value);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ToolCallStatusValues))]
    public void MapToolCallStatus_CoversEveryValue(dotacp.protocol.ToolCallStatus value)
    {
        ToolCallStatus mapped = SessionUpdateMapper.MapToolCallStatus(value);

        Assert.Equal(Enum.GetName(value), Enum.GetName(mapped));
    }

    public static TheoryData<dotacp.protocol.StopReason> StopReasonValues()
    {
        TheoryData<dotacp.protocol.StopReason> data = new TheoryData<dotacp.protocol.StopReason>();
        foreach (dotacp.protocol.StopReason value in Enum.GetValues<dotacp.protocol.StopReason>())
        {
            data.Add(value);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(StopReasonValues))]
    public void MapStopReason_CoversEveryValue(dotacp.protocol.StopReason value)
    {
        StopReason mapped = SessionUpdateMapper.MapStopReason(value);

        Assert.Equal(Enum.GetName(value), Enum.GetName(mapped));
    }

    public static TheoryData<dotacp.protocol.PermissionOptionKind> PermissionOptionKindValues()
    {
        TheoryData<dotacp.protocol.PermissionOptionKind> data = new TheoryData<dotacp.protocol.PermissionOptionKind>();
        foreach (dotacp.protocol.PermissionOptionKind value in Enum.GetValues<dotacp.protocol.PermissionOptionKind>())
        {
            data.Add(value);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(PermissionOptionKindValues))]
    public void MapPermissionOptionKind_CoversEveryValue(dotacp.protocol.PermissionOptionKind value)
    {
        PermissionOptionKind mapped = SessionUpdateMapper.MapPermissionOptionKind(value);

        Assert.Equal(Enum.GetName(value), Enum.GetName(mapped));
    }

    [Fact]
    public void MapPermission_BuildsContextWithOptionsInOrder()
    {
        dotacp.protocol.RequestPermissionRequest request = new dotacp.protocol.RequestPermissionRequest
        {
            SessionId = "s",
            ToolCall = new dotacp.protocol.ToolCallUpdate
            {
                ToolCallId = "call-1",
                Title = "Write hello.txt",
                Kind = dotacp.protocol.ToolKind.Edit,
                Status = dotacp.protocol.ToolCallStatus.Pending,
            },
            Options = new dotacp.protocol.PermissionOption[]
            {
                new dotacp.protocol.PermissionOption
                {
                    OptionId = "opt-a",
                    Name = "Allow once",
                    Kind = dotacp.protocol.PermissionOptionKind.AllowOnce,
                },
                new dotacp.protocol.PermissionOption
                {
                    OptionId = "opt-b",
                    Name = "Allow always",
                    Kind = dotacp.protocol.PermissionOptionKind.AllowAlways,
                },
                new dotacp.protocol.PermissionOption
                {
                    OptionId = "opt-c",
                    Name = "Reject",
                    Kind = dotacp.protocol.PermissionOptionKind.RejectOnce,
                },
            },
        };

        PermissionRequestContext context = SessionUpdateMapper.MapPermission(request);

        Assert.Equal("s", context.SessionId);
        Assert.Equal(ToolKind.Edit, context.ToolCall.Kind);
        Assert.Equal(
            new List<PermissionOptionInfo>
            {
                new PermissionOptionInfo("opt-a", "Allow once", PermissionOptionKind.AllowOnce),
                new PermissionOptionInfo("opt-b", "Allow always", PermissionOptionKind.AllowAlways),
                new PermissionOptionInfo("opt-c", "Reject", PermissionOptionKind.RejectOnce),
            },
            context.Options);
    }

    [Fact]
    public void SerializeRaw_Null_ReturnsNull()
    {
        string? result = SessionUpdateMapper.SerializeRaw(null);

        Assert.Null(result);
    }
}
