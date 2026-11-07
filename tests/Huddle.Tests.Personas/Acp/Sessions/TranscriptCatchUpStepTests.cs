using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions.Fakes;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// Pins <see cref="TranscriptCatchUpStep"/> on its own (D24): the first-Turn flag, the note it owes and what a
/// failed read leaves behind, without a <see cref="RoomSession"/> around it.
/// </summary>
public sealed class TranscriptCatchUpStepTests
{
    private const string RoomId = "room-1";

    /// <summary>A Command Turn reads nothing and leaves the first-Turn flag set, so the next Message Turn still reads the Transcript.</summary>
    [Fact]
    public async Task AddAsync_Command_DoesNotConsumeTheFlag()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeRoomSessionOwner owner = new();
        TranscriptCatchUpStep step = Build(owner);
        step.MarkOpened(resumed: false, resumedAfterId: null);

        await step.AddAsync(Item(WorkItemKind.Command), freshStartNotePending: true, ct);
        Assert.Empty(owner.TranscriptReadCalls);
        Assert.Null(step.TakeNote());

        WorkItem next = await step.AddAsync(Item(WorkItemKind.Message), freshStartNotePending: false, ct);

        Assert.Single(owner.TranscriptReadCalls);
        Assert.NotNull(next.Transcript);
    }

    /// <summary>A Greeting reads no Transcript but uses up the first-Turn opportunity, so a later Message Turn reads nothing (D24 correction 19).</summary>
    [Fact]
    public async Task AddAsync_Greeting_ConsumesTheFlagAndReadsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeRoomSessionOwner owner = new();
        TranscriptCatchUpStep step = Build(owner);
        step.MarkOpened(resumed: false, resumedAfterId: null);

        WorkItem greeting = await step.AddAsync(Item(WorkItemKind.Greeting), freshStartNotePending: false, ct);
        WorkItem later = await step.AddAsync(Item(WorkItemKind.Message), freshStartNotePending: false, ct);

        Assert.Null(greeting.Transcript);
        Assert.Null(later.Transcript);
        Assert.Empty(owner.TranscriptReadCalls);
    }

    /// <summary>A refused or timed-out read marks the owed note as failed and keeps the Turn's own-post lines, since no Transcript was set.</summary>
    [Fact]
    public async Task AddAsync_FailedRead_SetsReadFailedAndKeepsOwnPostLines()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeRoomSessionOwner owner = new()
        {
            ReadTranscriptHandler = (_, _, _, _, _) => Task.FromResult<TranscriptTail?>(null),
        };
        TranscriptCatchUpStep step = Build(owner);
        step.MarkOpened(resumed: false, resumedAfterId: null);
        WorkItem item = Item(WorkItemKind.Message) with { OwnPostLines = ["I said hi"] };

        WorkItem result = await step.AddAsync(item, freshStartNotePending: true, ct);

        Assert.Null(result.Transcript);
        Assert.Equal(["I said hi"], result.OwnPostLines);
        FreshStartNote? note = step.TakeNote();
        Assert.NotNull(note);
        Assert.True(note.ReadFailed);
        Assert.Null(step.TakeNote());
    }

    private static WorkItem Item(WorkItemKind kind) =>
        new(RoomId, "Room", "Ana", "hello", [], kind, TriggerMessageId: kind == WorkItemKind.Message ? "m9" : null);

    private static TranscriptCatchUpStep Build(FakeRoomSessionOwner owner) =>
        new(owner, new AcpOptions(), null, null, new FakePromptSource(), NullLogger.Instance, RoomId, static _ => false);
}
