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

    [Fact]
    public void CurrentModeUpdate_MapsModeChanged()
    {
        dotacp.protocol.CurrentModeUpdate update = new dotacp.protocol.CurrentModeUpdate { CurrentModeId = "plan" };

        AgentEvent result = SessionUpdateMapper.Map("s", update);

        Assert.Equal(new ModeChanged("s", "plan"), result);
    }

    [Fact]
    public void AvailableCommandsUpdate_MapsToUnknownUpdate()
    {
        dotacp.protocol.AvailableCommandsUpdate update = new dotacp.protocol.AvailableCommandsUpdate();

        AgentEvent result = SessionUpdateMapper.Map("s", update);

        Assert.Equal(new UnknownUpdate("s", "AvailableCommandsUpdate"), result);
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
