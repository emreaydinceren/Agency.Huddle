using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Library;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions.Fakes;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// Pins the whole path of a Prompt block through a <see cref="RoomSession"/> (design §6.6): what an
/// Adapter advertised, what its profile allows and what a Message names decide what reaches
/// <see cref="IAgentSession.PromptAsync(AgentPrompt, CancellationToken)"/>, and a Turn that is not an
/// ordinary Message can carry nothing.
/// </summary>
public sealed class RoomSessionPromptBlocksTests
{
    private static readonly Persona Nova = new("nova", "You are Nova.");

    private static readonly AgentPromptCapabilities ImageAndText = new(Image: true, EmbeddedContext: true);

    /// <summary>A path as a <c>file://</c> URL: the one form of an absolute path the collector recognises on every platform, so a test written on Windows also finds its files on Linux.</summary>
    private static string Link(string path) => new Uri(path).AbsoluteUri;

    private static AdapterProfile ProfileOf(bool readsFiles = true, bool promptBlocks = true) =>
        new("claude", "Claude", null, "node", null, null, true, ReadsFiles: readsFiles, PromptBlocks: promptBlocks);

    private static async Task<AgentPrompt> RunTurnAsync(
        LibraryFileServiceFixture fixture,
        WorkItem item,
        AgentPromptCapabilities? capabilities,
        bool readsFiles = true,
        bool promptBlocks = true,
        bool withHost = true,
        ILogger? logger = null)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        LibraryDocumentCollector collector = new(fixture.Resolver, fixture.CreateService(), Options.Create(new TeamOptions()));
        FakeAgentSession session = new();
        session.EnqueueReply("ok");
        FakePersonaHost? host = withHost
            ? new FakePersonaHost(session, ProfileOf(readsFiles, promptBlocks)) { PromptCapabilities = capabilities ?? AgentPromptCapabilities.None }
            : null;
        FakeRoomSessionOwner owner = new();
        using CancellationTokenSource runCts = new();
        await using RoomSession room = new(
            roomId: "room-1",
            open: _ => Task.FromResult<IAgentSession>(session),
            owner: owner,
            scheduler: new ImmediateTurnScheduler(),
            prompts: new FakePromptSource(),
            options: new AcpOptions(),
            fileChanges: null,
            declaredWatches: [],
            logger: logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
            runToken: runCts.Token,
            persona: Nova,
            host: host,
            libraryDocs: collector,
            readsFiles: readsFiles);

        room.Enqueue(new QueuedWork(1, item));
        await WaitUntilAsync(() => session.AgentPrompts.Count == 1, ct);
        await runCts.CancelAsync();
        return Assert.Single(session.AgentPrompts);
    }

    private static WorkItem MessageNaming(string text, IReadOnlyList<CaughtUpMessage>? missed = null) =>
        new("room-1", "Room 1", "Human", text, missed ?? [], TriggerMessageId: "trig-1");

    private static string WritePng(LibraryFileServiceFixture fixture, string name = "shot.png")
    {
        string path = Path.Combine(fixture.CreatePinnedRoot("Design"), name);
        File.WriteAllBytes(path, TestImages.Png(40, 30, 64));
        return path;
    }

    /// <summary>A Message that names a PNG, to an Adapter that advertised <c>image</c>, sends the text and exactly one image block, with the text first.</summary>
    [Fact]
    public async Task Turn_ImageNamedAndAdvertised_SendsTextThenOneImageBlock()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string png = WritePng(fixture);

        AgentPrompt prompt = await RunTurnAsync(fixture, MessageNaming($"what is wrong with {Link(png)}"), ImageAndText);

        AgentImageBlock block = Assert.IsType<AgentImageBlock>(Assert.Single(prompt.Blocks!));
        Assert.Equal("image/png", block.MimeType);
        Assert.Equal(File.ReadAllBytes(png), block.Data.ToArray());
        Assert.Contains("included with this message", prompt.Text, StringComparison.Ordinal); // contains-ok: prompt text, not markup
        Assert.Contains(png, prompt.Text, StringComparison.Ordinal); // contains-ok: prompt text, not markup
    }

    /// <summary>An Adapter that advertised nothing receives today's prompt: no block, and an ordinary path line.</summary>
    [Fact]
    public async Task Turn_ImageNamedNothingAdvertised_SendsNoBlocksAndAPathLine()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string png = WritePng(fixture);

        AgentPrompt prompt = await RunTurnAsync(fixture, MessageNaming($"look {Link(png)}"), AgentPromptCapabilities.None);

        Assert.Empty(prompt.Blocks!);
        Assert.DoesNotContain("included with this message", prompt.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("an image you cannot see", prompt.Text, StringComparison.Ordinal);
    }

    /// <summary>The profile's <c>PromptBlocks: false</c> turns blocks off even for an Adapter that advertised images.</summary>
    [Fact]
    public async Task Turn_PromptBlocksFalse_SendsNoBlocks()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string png = WritePng(fixture);

        AgentPrompt prompt = await RunTurnAsync(fixture, MessageNaming($"look {Link(png)}"), ImageAndText, promptBlocks: false);

        Assert.Empty(prompt.Blocks!);
    }

    /// <summary>A Room Session with no host, as every caller that predates resume has, sends no blocks.</summary>
    [Fact]
    public async Task Turn_NoHost_SendsNoBlocks()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string png = WritePng(fixture);

        AgentPrompt prompt = await RunTurnAsync(fixture, MessageNaming($"look {Link(png)}"), null, withHost: false);

        Assert.Empty(prompt.Blocks!);
    }

    /// <summary>A PNG named only in catch-up, not in the Message that started the Turn, is not sent as a block (D-2).</summary>
    [Fact]
    public async Task Turn_ImageOnlyInCatchUp_SendsNoBlocks()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string png = WritePng(fixture);

        AgentPrompt prompt = await RunTurnAsync(fixture, MessageNaming("and now?", [new CaughtUpMessage("Friend", $"look {Link(png)}")]), ImageAndText);

        Assert.Empty(prompt.Blocks!);
        Assert.Contains(png, prompt.Text, StringComparison.Ordinal); // contains-ok: prompt text, not markup
    }

    /// <summary>An Adapter with no file tools and no image support is told it cannot see the image.</summary>
    [Fact]
    public async Task Turn_NoFileToolsNoImages_SaysAnImageYouCannotSee()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string png = WritePng(fixture);

        AgentPrompt prompt = await RunTurnAsync(fixture, MessageNaming($"look {Link(png)}"), AgentPromptCapabilities.None, readsFiles: false);

        Assert.Empty(prompt.Blocks!);
        Assert.Contains("an image you cannot see", prompt.Text, StringComparison.Ordinal); // contains-ok: prompt text, not markup
    }

    /// <summary>An Adapter with no file tools that advertised <c>embeddedContext</c> gets a Markdown file's text as a resource block, and the text is not also inlined.</summary>
    [Fact]
    public async Task Turn_NoFileToolsEmbeddedContext_SendsTextAsResource()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string notes = Path.Combine(fixture.CreatePinnedRoot("Design"), "notes.md");
        File.WriteAllText(notes, "# the document's own words");

        AgentPrompt prompt = await RunTurnAsync(fixture, MessageNaming($"read {Link(notes)}"), new AgentPromptCapabilities(Image: false, EmbeddedContext: true), readsFiles: false);

        AgentTextResourceBlock block = Assert.IsType<AgentTextResourceBlock>(Assert.Single(prompt.Blocks!));
        Assert.Equal("# the document's own words", block.Text);
        Assert.DoesNotContain("# the document's own words", prompt.Text, StringComparison.Ordinal);
    }

    /// <summary>An Adapter that reads files never gets a document's text as a resource, even when it advertised <c>embeddedContext</c>.</summary>
    [Fact]
    public async Task Turn_ReadsFilesEmbeddedContext_SendsNoResource()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string notes = Path.Combine(fixture.CreatePinnedRoot("Design"), "notes.md");
        File.WriteAllText(notes, "# words");

        AgentPrompt prompt = await RunTurnAsync(fixture, MessageNaming($"read {Link(notes)}"), ImageAndText, readsFiles: true);

        Assert.Empty(prompt.Blocks!);
    }

    /// <summary>A Command Turn is the bare command and carries no block, even if its text names an image.</summary>
    [Fact]
    public async Task CommandTurn_CarriesNoBlocksAndNoLibraryLines()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string png = WritePng(fixture);
        WorkItem command = new("room-1", "Room 1", "Human", $"@nova /compact {Link(png)}", [], WorkItemKind.Command, TriggerMessageId: "trig-1", Command: new AdapterCommandCall("compact", string.Empty));

        AgentPrompt prompt = await RunTurnAsync(fixture, command, ImageAndText);

        Assert.Equal("/compact", prompt.Text);
        Assert.Empty(prompt.Blocks!);
    }

    /// <summary>A Greeting carries no block.</summary>
    [Fact]
    public async Task GreetingTurn_CarriesNoBlocks()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        _ = WritePng(fixture);
        WorkItem greeting = new("room-1", "Room 1", string.Empty, string.Empty, [], WorkItemKind.Greeting);

        AgentPrompt prompt = await RunTurnAsync(fixture, greeting, ImageAndText);

        Assert.Empty(prompt.Blocks!);
    }

    /// <summary>The Turn still runs when an image is withheld, and one Information line records counts by reason without the path.</summary>
    [Fact]
    public async Task Turn_ImageWithheld_LogsOneInformationLineAndCarriesOn()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("Design");
        string fake = Path.Combine(root, "shot.png");
        File.WriteAllBytes(fake, TestImages.Png(40, 30, 3 * 1024 * 1024 + 10));
        RecordingLogger logger = new();

        AgentPrompt prompt = await RunTurnAsync(fixture, MessageNaming($"look {Link(fake)}"), ImageAndText, logger: logger);

        Assert.Empty(prompt.Blocks!);
        (LogLevel Level, string Message) entry = Assert.Single(logger.Entries, e => e.Message.Contains("path lines instead of blocks", StringComparison.Ordinal)); // contains-ok: log text
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("TooLarge=1", entry.Message, StringComparison.Ordinal); // contains-ok: log text
        Assert.DoesNotContain(fake, entry.Message, StringComparison.Ordinal);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken ct)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Condition was not met before the test's timeout.");
            }

            await Task.Delay(10, ct);
        }
    }

    private sealed class RecordingLogger : ILogger
    {
        private readonly Lock gate = new();

        private readonly List<(LogLevel Level, string Message)> entries = [];

        public IReadOnlyList<(LogLevel Level, string Message)> Entries
        {
            get
            {
                lock (this.gate)
                {
                    return [.. this.entries];
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            lock (this.gate)
            {
                this.entries.Add((logLevel, formatter(state, exception)));
            }
        }
    }
}
