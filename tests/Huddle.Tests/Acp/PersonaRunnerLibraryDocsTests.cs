namespace Agency.Huddle.Tests.Acp;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Library;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions.Fakes;
using Agency.Huddle.Tests.Library;

/// <summary>
/// Pins Task 10.4: the plumbing that carries a <see cref="LibraryDocumentCollector"/> and a
/// Persona's resolved <c>readsFiles</c> flag from wherever a <see cref="RoomSession"/> is built down
/// to a Turn's own prompt (Spec §6.14). Built directly against <see cref="RoomSession"/>, the
/// innermost of the four types Task 10.4.i adds trailing optional parameters to - the same level
/// <see cref="Agency.Huddle.Tests.Acp.Sessions.TranscriptCatchUpTests"/> already tests the Transcript
/// Catch-up plumbing at, and lighter than a full <see cref="Agency.Huddle.Tests.Pipes.PipeHostFixture"/>
/// run for the same reason: no real pipe, no real adapter process, deterministic completion.
/// </summary>
public sealed class PersonaRunnerLibraryDocsTests
{
    private static readonly Persona Nova = new("nova", "You are Nova.");

    /// <summary>A path in the triggering Message's own text is collected, and the Library documents block appears in the prompt.</summary>
    [Fact]
    public async Task Turn_MessageWithLibraryPath_PromptHasBlock()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string rootPath = fixture.CreatePinnedRoot("Root1");
        string filePath = Path.Combine(rootPath, "doc.md");
        File.WriteAllText(filePath, "doc contents");
        string uri = new Uri(filePath).AbsoluteUri;
        LibraryDocumentCollector collector = new(fixture.Resolver, fixture.CreateService(), Options.Create(new TeamOptions()));

        FakeAgentSession session = new();
        session.EnqueueReply("ok");
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session), libraryDocs: collector, readsFiles: true);
        try
        {
            room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", "Human", $"see {uri}", [], TriggerMessageId: "trig-1")));
            await WaitUntilAsync(() => session.Prompts.Count == 1, ct);

            var prompt = Assert.Single(session.Prompts);
            var header = new FakePromptSource().Render("turn.libraryDocsHeader", new Dictionary<string, string>());
            Assert.Contains(header, prompt, StringComparison.Ordinal); // contains-ok: prompt text, not markup // contains-ok: prompt text, not markup
            Assert.Contains(filePath, prompt, StringComparison.Ordinal); // contains-ok: prompt text, not markup
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>With <c>readsFiles: false</c>, the kept document's decoded text is inlined into the prompt.</summary>
    [Fact]
    public async Task Turn_ReadsFilesFalse_PromptInlinesText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string rootPath = fixture.CreatePinnedRoot("Root1");
        string filePath = Path.Combine(rootPath, "doc.md");
        File.WriteAllText(filePath, "the document's own inlined content");
        string uri = new Uri(filePath).AbsoluteUri;
        LibraryDocumentCollector collector = new(fixture.Resolver, fixture.CreateService(), Options.Create(new TeamOptions()));

        FakeAgentSession session = new();
        session.EnqueueReply("ok");
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session), libraryDocs: collector, readsFiles: false);
        try
        {
            room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", "Human", $"see {uri}", [], TriggerMessageId: "trig-1")));
            await WaitUntilAsync(() => session.Prompts.Count == 1, ct);

            var prompt = Assert.Single(session.Prompts);
            Assert.Contains("the document's own inlined content", prompt, StringComparison.Ordinal); // contains-ok: prompt text, not markup
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>With <c>readsFiles: true</c>, the kept document's own text is never inlined - only its path, location and size line.</summary>
    [Fact]
    public async Task Turn_ReadsFilesTrue_NoText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string rootPath = fixture.CreatePinnedRoot("Root1");
        string filePath = Path.Combine(rootPath, "doc.md");
        File.WriteAllText(filePath, "must not be inlined");
        string uri = new Uri(filePath).AbsoluteUri;
        LibraryDocumentCollector collector = new(fixture.Resolver, fixture.CreateService(), Options.Create(new TeamOptions()));

        FakeAgentSession session = new();
        session.EnqueueReply("ok");
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session), libraryDocs: collector, readsFiles: true);
        try
        {
            room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", "Human", $"see {uri}", [], TriggerMessageId: "trig-1")));
            await WaitUntilAsync(() => session.Prompts.Count == 1, ct);

            var prompt = Assert.Single(session.Prompts);
            Assert.Contains(filePath, prompt, StringComparison.Ordinal); // contains-ok: prompt text, not markup
            Assert.DoesNotContain("must not be inlined", prompt, StringComparison.Ordinal);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>A path only in a catch-up Message (<see cref="WorkItem.MissedMessages"/>), not in the triggering Message's own text, is still collected (corrections-B6 item 7).</summary>
    [Fact]
    public async Task Turn_CatchUpMessagesAlsoScanned()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string rootPath = fixture.CreatePinnedRoot("Root1");
        string filePath = Path.Combine(rootPath, "doc.md");
        File.WriteAllText(filePath, "doc contents");
        string uri = new Uri(filePath).AbsoluteUri;
        LibraryDocumentCollector collector = new(fixture.Resolver, fixture.CreateService(), Options.Create(new TeamOptions()));

        FakeAgentSession session = new();
        session.EnqueueReply("ok");
        FakeRoomSessionOwner owner = new();
        owner.ReadTranscriptHandler = (roomId, _, _, _, _) => Task.FromResult<TranscriptTail?>(null);
        var (room, runCts) = CreateSession(owner, FixedOpen(session), libraryDocs: collector, readsFiles: true);
        try
        {
            room.Enqueue(new QueuedWork(
                1,
                new WorkItem(
                    "room-1", "Room 1", "Human", "hi", [new CaughtUpMessage("Friend", $"see {uri}")], TriggerMessageId: "trig-1")));
            await WaitUntilAsync(() => session.Prompts.Count == 1, ct);

            var prompt = Assert.Single(session.Prompts);
            var header = new FakePromptSource().Render("turn.libraryDocsHeader", new Dictionary<string, string>());
            Assert.Contains(header, prompt, StringComparison.Ordinal); // contains-ok: prompt text, not markup
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>A path only in this Agent's own earlier post-message lines (<see cref="WorkItem.OwnPostLines"/>) is NOT scanned (corrections-B6 item 7).</summary>
    [Fact]
    public async Task Turn_OwnPostLines_NotScanned()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string rootPath = fixture.CreatePinnedRoot("Root1");
        string filePath = Path.Combine(rootPath, "doc.md");
        File.WriteAllText(filePath, "doc contents");
        string uri = new Uri(filePath).AbsoluteUri;
        LibraryDocumentCollector collector = new(fixture.Resolver, fixture.CreateService(), Options.Create(new TeamOptions()));

        FakeAgentSession session = new();
        session.EnqueueReply("ok");
        FakeRoomSessionOwner owner = new();

        // A refused Transcript read (tail null) leaves OwnPostLines untouched (see RoomSession's own
        // comment beside the Transcript block), so this is the only way to reach the read loop's
        // OwnPostLines path directly for this test's purpose.
        owner.ReadTranscriptHandler = (roomId, _, _, _, _) => Task.FromResult<TranscriptTail?>(null);
        var (room, runCts) = CreateSession(owner, FixedOpen(session), libraryDocs: collector, readsFiles: true);
        try
        {
            room.Enqueue(new QueuedWork(
                1,
                new WorkItem(
                    "room-1", "Room 1", "Human", "hi", [], TriggerMessageId: "trig-1", OwnPostLines: [$"see {uri}"])));
            await WaitUntilAsync(() => session.Prompts.Count == 1, ct);

            var prompt = Assert.Single(session.Prompts);
            var header = new FakePromptSource().Render("turn.libraryDocsHeader", new Dictionary<string, string>());
            Assert.DoesNotContain(header, prompt, StringComparison.Ordinal);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>A path only in the first-Turn Transcript Catch-up (<see cref="WorkItem.Transcript"/>) is collected (corrections-B6 item 7).</summary>
    [Fact]
    public async Task Turn_TranscriptMessagesScanned()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string rootPath = fixture.CreatePinnedRoot("Root1");
        string filePath = Path.Combine(rootPath, "doc.md");
        File.WriteAllText(filePath, "doc contents");
        string uri = new Uri(filePath).AbsoluteUri;
        LibraryDocumentCollector collector = new(fixture.Resolver, fixture.CreateService(), Options.Create(new TeamOptions()));

        FakeAgentSession session = new();
        session.EnqueueReply("ok");
        FakeRoomSessionOwner owner = new();
        owner.ReadTranscriptHandler = (roomId, _, _, _, _) => Task.FromResult<TranscriptTail?>(
            new TranscriptTail("req", roomId, [new ChatMessage("m1", DateTimeOffset.UtcNow, "hu", "Human", $"see {uri}")], Omitted: 0));
        var (room, runCts) = CreateSession(owner, FixedOpen(session), libraryDocs: collector, readsFiles: true);
        try
        {
            room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", "Human", "go on", [], TriggerMessageId: "trig-1")));
            await WaitUntilAsync(() => session.Prompts.Count == 1, ct);

            var prompt = Assert.Single(session.Prompts);
            var header = new FakePromptSource().Render("turn.libraryDocsHeader", new Dictionary<string, string>());
            Assert.Contains(header, prompt, StringComparison.Ordinal); // contains-ok: prompt text, not markup
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>Builds a per-Room <see cref="RoomSession"/> with fakes standing in for every collaborator except resume, matching <see cref="Agency.Huddle.Tests.Acp.Sessions.TranscriptCatchUpTests"/>'s own helper.</summary>
    private static (RoomSession Session, CancellationTokenSource RunCts) CreateSession(
        FakeRoomSessionOwner owner,
        Func<CancellationToken, Task<IAgentSession>> open,
        LibraryDocumentCollector? libraryDocs = null,
        bool readsFiles = true,
        AcpOptions? options = null)
    {
        CancellationTokenSource runCts = new();
        RoomSession session = new(
            roomId: "room-1",
            open: open,
            owner: owner,
            scheduler: new ImmediateTurnScheduler(),
            prompts: new FakePromptSource(),
            options: options ?? new AcpOptions(),
            fileChanges: null,
            declaredWatches: [],
            logger: NullLogger.Instance,
            runToken: runCts.Token,
            persona: Nova,
            libraryDocs: libraryDocs,
            readsFiles: readsFiles);
        return (session, runCts);
    }

    /// <summary>An open delegate that always returns the same session.</summary>
    private static Func<CancellationToken, Task<IAgentSession>> FixedOpen(FakeAgentSession session) =>
        _ => Task.FromResult<IAgentSession>(session);

    private static async Task DisposeSessionAsync(RoomSession session, CancellationTokenSource runCts)
    {
        await runCts.CancelAsync();
        await session.DisposeAsync();
        runCts.Dispose();
    }

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken ct, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Condition was not met before the test's timeout.");
            }

            await Task.Delay(10, ct);
        }
    }
}
