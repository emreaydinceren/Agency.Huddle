using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Acp.Sessions.TurnHooks;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions.Fakes;
using Agency.Huddle.Tests.Waiting;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// Pins what a clean <see cref="RoomSession"/> (a goldfish possession's, Goldfish corrections slice 2, A9) leaves out of a Turn: no
/// Transcript Catch-up, no last-catch-up file, no fresh-start note, and no Turn hook; and what it adds: the enrichers are told the Turn is
/// clean. An ordinary session beside each row is the control that proves the row can fail.
/// </summary>
public sealed class CleanRoomSessionTests
{
    private const string RoomId = "room-1";
    private const string BriefText = "brief: tidy the garden";

    private static readonly Persona Nova = new("nova", "You are Nova.", Model: "m1", Effort: "e1");

    private static readonly ChatMessage Earlier = new("m0", DateTimeOffset.UnixEpoch, "hu", "Human", "an earlier room message");

    /// <summary>A clean session's first prompt is the item's own text: the Room's earlier Messages are never read or shown.</summary>
    [Fact]
    public async Task CleanSession_FirstTurn_PromptHasNoTranscriptAndNoRead()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Rig rig = new(clean: true);
        await using RoomSessionHarness harness = rig.Build();

        harness.Room.Enqueue(new QueuedWork(1, Item()));
        await Eventually.UntilAsync(() => rig.Session.Prompts.Count == 1, ct, timeout: TestBudgets.Standard);

        string prompt = rig.Session.Prompts[0];
        // contains-ok: prompt text, not markup; the layout of the Transcript block is pinned by the Catch-up tests.
        Assert.Contains(BriefText, prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("an earlier room message", prompt, StringComparison.Ordinal);
        Assert.Empty(rig.Owner.TranscriptReadCalls);
    }

    /// <summary>The control: the same session with <c>clean</c> off reads the Transcript and carries the Room's earlier Message in its first prompt.</summary>
    [Fact]
    public async Task OrdinarySession_FirstTurn_PromptCarriesTheCatchUp()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Rig rig = new(clean: false);
        await using RoomSessionHarness harness = rig.Build();

        harness.Room.Enqueue(new QueuedWork(1, Item()));
        await Eventually.UntilAsync(() => rig.Session.Prompts.Count == 1, ct, timeout: TestBudgets.Standard);

        string prompt = rig.Session.Prompts[0];
        // contains-ok: prompt text, not markup; the layout of the Transcript block is pinned by the Catch-up tests.
        Assert.Contains("an earlier room message", prompt, StringComparison.Ordinal);
        Assert.Single(rig.Owner.TranscriptReadCalls);
    }

    /// <summary>A clean session's first Turn writes no last-catch-up file, where an ordinary one writes it.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task FirstTurn_WritesTheLastCatchUpFileOnlyWhenNotClean(bool clean, bool expectedFile)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        LastCatchUpStore store = new(dataDir.Options(), NullLogger<LastCatchUpStore>.Instance);
        Rig rig = new(clean) { LastCatchUp = store };
        await using RoomSessionHarness harness = rig.Build();
        harness.Room.MarkFreshStartNotePending(); // The file records the first Turn after a fresh start, which is when a note is owed.

        harness.Room.Enqueue(new QueuedWork(1, Item()));
        await Eventually.UntilAsync(() => rig.Session.Prompts.Count == 1 && harness.Room.State == RoomSessionState.Idle, ct, timeout: TestBudgets.Standard);

        Assert.Equal(expectedFile, store.Has("nova", RoomId));
    }

    /// <summary>A clean session owes no fresh-start note even when one is marked pending, where an ordinary one posts it.</summary>
    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public async Task FreshStartNotePending_PostsTheNoteOnlyWhenNotClean(bool clean, int expectedNotes)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Rig rig = new(clean);
        await using RoomSessionHarness harness = rig.Build();
        harness.Room.MarkFreshStartNotePending();

        harness.Room.Enqueue(new QueuedWork(1, Item()));
        await Eventually.UntilAsync(() => rig.Session.Prompts.Count == 1 && harness.Room.State == RoomSessionState.Idle, ct, timeout: TestBudgets.Standard);

        Assert.Equal(expectedNotes, rig.Owner.Written.OfType<FreshStartNote>().Count());
    }

    /// <summary>A pipeline made clean holds no hook, so a clean session's Message Turn runs neither Before nor After.</summary>
    [Fact]
    public async Task CleanSession_WithAForCleanPipeline_RunsNoHook()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        CountingHook hook = new();
        TurnHookPipeline ordinary = new([hook], new TurnHookScope("nova", ReadsFiles: true, FileChangesOn: false, DeclaredWatches: []), NullLogger.Instance);
        Rig rig = new(clean: true) { Hooks = ordinary.ForClean() };
        await using RoomSessionHarness harness = rig.Build();

        harness.Room.Enqueue(new QueuedWork(1, Item()));
        await Eventually.UntilAsync(() => rig.Session.Prompts.Count == 1 && harness.Room.State == RoomSessionState.Idle, ct, timeout: TestBudgets.Standard);

        Assert.Equal(0, hook.Calls);
    }

    /// <summary>The enrichers are told the Turn is clean exactly when the session is.</summary>
    /// <param name="clean">Whether the session is clean.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EnrichAsync_ReceivesTheSessionsCleanFlag(bool clean)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingEnricher enricher = new();
        Rig rig = new(clean) { Enrichers = TurnEnricherPipeline.ForPersona([enricher], NullLogger.Instance) };
        await using RoomSessionHarness harness = rig.Build();

        harness.Room.Enqueue(new QueuedWork(1, Item()));
        await Eventually.UntilAsync(() => rig.Session.Prompts.Count == 1, ct, timeout: TestBudgets.Standard);

        Assert.Equal([clean], enricher.CleanFlags);
    }

    private static WorkItem Item() =>
        new(RoomId, "Room 1", "Human", BriefText, [], TriggerMessageId: "trig-1");

    /// <summary>One session over a fake agent session and a Transcript holding <see cref="Earlier"/>.</summary>
    private sealed class Rig
    {
        private readonly bool clean;

        public Rig(bool clean)
        {
            this.clean = clean;
            this.Session.EnqueueReply("done");
            this.Owner = new FakeRoomSessionOwner
            {
                ReadTranscriptHandler = static (roomId, _, _, _, _) =>
                    Task.FromResult<TranscriptTail?>(new TranscriptTail("req", roomId, [Earlier], Omitted: 0)),
            };
        }

        public FakeAgentSession Session { get; } = new();

        public FakeRoomSessionOwner Owner { get; }

        public LastCatchUpStore? LastCatchUp { get; init; }

        public TurnHookPipeline? Hooks { get; init; }

        public TurnEnricherPipeline? Enrichers { get; init; }

        public RoomSessionHarness Build() =>
            RoomSessionHarness.Create(
                this.Owner,
                RoomSessionHarness.FixedOpen(this.Session),
                new RoomSessionHarnessOptions
                {
                    Persona = Nova,
                    AgentId = "agent-1",
                    Clean = this.clean,
                    LastCatchUp = this.LastCatchUp,
                    Hooks = this.Hooks,
                    Enrichers = this.Enrichers,
                });
    }

    /// <summary>A hook that counts every call to either point.</summary>
    private sealed class CountingHook : ITurnHook
    {
        private int calls;

        public int Calls => Volatile.Read(ref this.calls);

        public string Name => "counting";

        public bool AppliesTo(TurnHookScope scope) => !scope.Clean;

        public Task<string?> OnSessionOpeningAsync(SessionOpening opening, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref this.calls);
            return Task.FromResult<string?>(null);
        }

        public Task<TurnAddition?> BeforeTurnAsync(TurnStarting turn, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref this.calls);
            return Task.FromResult<TurnAddition?>(null);
        }

        public Task<string?> AfterTurnAsync(TurnEnded turn, TurnAddition? addition, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref this.calls);
            return Task.FromResult<string?>(null);
        }
    }

    /// <summary>An enricher, under a production-order name, that records the clean flag of each Turn it sees.</summary>
    private sealed class RecordingEnricher : ITurnEnricher
    {
        private readonly Lock gate = new();
        private readonly List<bool> cleanFlags = [];

        public IReadOnlyList<bool> CleanFlags
        {
            get
            {
                lock (this.gate)
                {
                    return [.. this.cleanFlags];
                }
            }
        }

        public string Name => LibraryDocumentsEnricher.EnricherName;

        public Task<WorkItem> EnrichAsync(TurnEnriching turn, CancellationToken cancellationToken)
        {
            lock (this.gate)
            {
                this.cleanFlags.Add(turn.Clean);
            }

            return Task.FromResult(turn.Item);
        }
    }
}
