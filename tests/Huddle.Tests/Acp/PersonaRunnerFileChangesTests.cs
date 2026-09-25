using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Pipes;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Exercises FC §6.8 end to end: <see cref="PersonaRunner"/> built over a real pipe (via
/// <see cref="PipeHostFixture"/>) with a real <see cref="FileChangeTracker"/> from the fixture's own
/// container, against a <see cref="FakeAgentHostFactory"/>, so no test spends a token or launches
/// <c>node</c>.
/// </summary>
public sealed class PersonaRunnerFileChangesTests
{
    /// <summary>With no baseline yet, the first Turn's prompt carries no File Changes block: byte-identical to a plain Turn (FC §6.8 item 1, F7).</summary>
    [Fact]
    public async Task FirstTurn_NoBlock_PromptByteIdenticalToToday()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("ok");
        var persona = new Persona("nova", "You are Nova.");
        var tracker = fixture.Services.GetRequiredService<FileChangeTracker>();

        await using var agentHost = CreateHost(fixture, persona, factory, tracker);
        await agentHost.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        var directory = fixture.Services.GetRequiredService<ITeamDirectory>();

        await chat.PostAsync(roomId, KnownIds.Human, "hi", ct: ct);
        await WaitForHistoryCountAsync(store, roomId, 2, ct);

        var room = await directory.GetRoomAsync(roomId, ct);
        Assert.NotNull(room);
        var expected = RoomSession.BuildPrompt(new WorkItem(roomId, room.Name, "You", "hi", []), new FakePromptSource());

        Assert.Single(factory.Session.Prompts);
        Assert.Equal(expected, factory.Session.Prompts[0]);
    }

    /// <summary>A file changed between two Turns is listed, and the File Changes block opens the prompt (FC §6.8, §6.13).</summary>
    [Fact]
    public async Task SecondTurn_FileChangedBetween_BlockIsFirstInPrompt()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var workDir = OwnWorkDir(options.Value, "nova");
        Directory.CreateDirectory(workDir);
        var filePath = Path.Combine(workDir, "a.txt");
        File.WriteAllText(filePath, "v1");

        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("first");
        factory.Session.EnqueueReply("second");
        var persona = new Persona("nova", "You are Nova.");
        var tracker = fixture.Services.GetRequiredService<FileChangeTracker>();

        await using var agentHost = CreateHost(fixture, persona, factory, tracker);
        await agentHost.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();

        await chat.PostAsync(roomId, KnownIds.Human, "hi", ct: ct);
        await WaitForHistoryCountAsync(store, roomId, 2, ct);

        File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddMinutes(5));
        File.WriteAllText(filePath, "v1 changed");

        await chat.PostAsync(roomId, KnownIds.Human, "hi again", ct: ct);
        await WaitForHistoryCountAsync(store, roomId, 4, ct);

        var header = new FakePromptSource().Render("turn.fileChangesHeader", new Dictionary<string, string>());

        Assert.Equal(2, factory.Session.Prompts.Count);
        Assert.DoesNotContain(header, factory.Session.Prompts[0], StringComparison.Ordinal);
        Assert.StartsWith(header, factory.Session.Prompts[1], StringComparison.Ordinal);
    }

    /// <summary>
    /// F0, the headline test: an edit this Agent made itself, attributed via a <see cref="ToolKind.Edit"/>
    /// tool call, is not listed back in the Room it was made in, but is listed in another Room this
    /// Agent is also in (FC §6.7, §6.8 item 2).
    /// </summary>
    [Fact]
    public async Task EditToolCallInRoomA_FileUnlistedInA_ListedInB()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var workDir = OwnWorkDir(options.Value, "nova");
        Directory.CreateDirectory(workDir);
        var filePath = Path.Combine(workDir, "a.txt");
        File.WriteAllText(filePath, "v1");

        var factory = new FakeAgentHostFactory();
        var tracker = fixture.Services.GetRequiredService<FileChangeTracker>();
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory, tracker);
        await agentHost.StartAsync(ct);

        var (novaId, roomAId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        var directory = fixture.Services.GetRequiredService<ITeamDirectory>();
        var drafts = fixture.Services.GetRequiredService<Drafts>();

        var roomB = await directory.CreateRoomAsync("Room B", [KnownIds.Human, novaId], ct);

        // Seed both Rooms' baselines with the file's original content.
        factory.Session.EnqueueReply("seed a");
        await chat.PostAsync(roomAId, KnownIds.Human, "hi a", ct: ct);
        await WaitForHistoryCountAsync(store, roomAId, 2, ct);

        factory.Session.EnqueueReply("seed b");
        await chat.PostAsync(roomB.Id, KnownIds.Human, "hi b", ct: ct);
        await WaitForHistoryCountAsync(store, roomB.Id, 2, ct);

        // Room A's second Turn: an Edit tool call touches a.txt while the Turn is running - the file
        // is written mid-turn, in the gap EnqueueDripFedReply leaves before its one chunk.
        var rawInput = RawInputFor(filePath);
        factory.Session.EnqueueToolActivity(
            new ToolCallStarted(factory.Session.SessionId, "c1", "Write", ToolKind.Edit, ToolCallStatus.Pending, rawInput),
            new ToolCallUpdated(factory.Session.SessionId, "c1", "Write", ToolKind.Edit, ToolCallStatus.Completed, null, rawInput));
        factory.Session.EnqueueDripFedReply(TimeSpan.FromMilliseconds(300), "edited a");

        await chat.PostAsync(roomAId, KnownIds.Human, "edit please", ct: ct);

        await WaitForToolStatusAsync(drafts, roomAId, ToolActivityStatus.Completed, ct);
        File.WriteAllText(filePath, "v2 edited");

        await WaitForHistoryCountAsync(store, roomAId, 4, ct);

        // Room A's next Turn: the edit was attributed to this Agent's own tool call and must not be listed.
        factory.Session.EnqueueReply("a again");
        await chat.PostAsync(roomAId, KnownIds.Human, "hi a again", ct: ct);
        await WaitForHistoryCountAsync(store, roomAId, 6, ct);

        // Room B's next Turn: the same edit, never attributed there, must be listed.
        factory.Session.EnqueueReply("b again");
        await chat.PostAsync(roomB.Id, KnownIds.Human, "hi b again", ct: ct);
        await WaitForHistoryCountAsync(store, roomB.Id, 4, ct);

        Assert.Equal(5, factory.Session.Prompts.Count);
        Assert.DoesNotContain(filePath, factory.Session.Prompts[3], StringComparison.Ordinal);
        Assert.Contains(filePath, factory.Session.Prompts[4], StringComparison.Ordinal);
    }

    /// <summary>An <see cref="ToolKind.Execute"/> call is never parsed for paths (FC D-3b): an edit it caused is not attributed, and is listed back in the same Room next Turn like any other drift.</summary>
    [Fact]
    public async Task ExecuteToolCall_NotAttributed_ListedBackInSameRoom()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var workDir = OwnWorkDir(options.Value, "nova");
        Directory.CreateDirectory(workDir);
        var filePath = Path.Combine(workDir, "a.txt");
        File.WriteAllText(filePath, "v1");

        var factory = new FakeAgentHostFactory();
        var tracker = fixture.Services.GetRequiredService<FileChangeTracker>();
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory, tracker);
        await agentHost.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        var drafts = fixture.Services.GetRequiredService<Drafts>();

        factory.Session.EnqueueReply("seed");
        await chat.PostAsync(roomId, KnownIds.Human, "hi", ct: ct);
        await WaitForHistoryCountAsync(store, roomId, 2, ct);

        var rawInput = RawInputFor(filePath);
        factory.Session.EnqueueToolActivity(
            new ToolCallStarted(factory.Session.SessionId, "c1", "Bash", ToolKind.Execute, ToolCallStatus.Pending, rawInput),
            new ToolCallUpdated(factory.Session.SessionId, "c1", "Bash", ToolKind.Execute, ToolCallStatus.Completed, null, rawInput));
        factory.Session.EnqueueDripFedReply(TimeSpan.FromMilliseconds(300), "ran a command");

        await chat.PostAsync(roomId, KnownIds.Human, "run it", ct: ct);

        await WaitForToolStatusAsync(drafts, roomId, ToolActivityStatus.Completed, ct);
        File.WriteAllText(filePath, "v2 edited");

        await WaitForHistoryCountAsync(store, roomId, 4, ct);

        factory.Session.EnqueueReply("seed again");
        await chat.PostAsync(roomId, KnownIds.Human, "hi again", ct: ct);
        await WaitForHistoryCountAsync(store, roomId, 6, ct);

        Assert.Equal(3, factory.Session.Prompts.Count);
        Assert.Contains(filePath, factory.Session.Prompts[2], StringComparison.Ordinal);
    }

    /// <summary>FC §9 E-5, finding P-17: a Turn refused outright, before any event, never commits - the Room's list repeats on the very next Turn.</summary>
    [Fact]
    public async Task PromptFailsImmediately_NotCommitted_ListRepeatsNextTurn()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var workDir = OwnWorkDir(options.Value, "nova");
        Directory.CreateDirectory(workDir);
        var filePath = Path.Combine(workDir, "a.txt");
        File.WriteAllText(filePath, "v1");

        var factory = new FakeAgentHostFactory();
        var tracker = fixture.Services.GetRequiredService<FileChangeTracker>();
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory, tracker);
        await agentHost.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();

        factory.Session.EnqueueReply("seed");
        await chat.PostAsync(roomId, KnownIds.Human, "hi", ct: ct);
        await WaitForHistoryCountAsync(store, roomId, 2, ct);

        File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddMinutes(5));
        File.WriteAllText(filePath, "v1 changed");

        factory.Session.EnqueueFailure(new AgentException("session/prompt failed: refused"));
        await chat.PostAsync(roomId, KnownIds.Human, "fail please", ct: ct);
        await WaitForHistoryCountAsync(store, roomId, 3, ct);

        factory.Session.EnqueueReply("ok again");
        await chat.PostAsync(roomId, KnownIds.Human, "hi again", ct: ct);
        await WaitForHistoryCountAsync(store, roomId, 5, ct);

        Assert.Equal(3, factory.Session.Prompts.Count);
        Assert.Contains(filePath, factory.Session.Prompts[1], StringComparison.Ordinal);
        Assert.Contains(filePath, factory.Session.Prompts[2], StringComparison.Ordinal);
    }

    /// <summary>FC §9 E-6, finding P-17: a Turn the Human stopped still commits, because it saw activity (a tool call) before the Stop landed.</summary>
    [Fact]
    public async Task StoppedTurnAfterActivity_Committed()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var workDir = OwnWorkDir(options.Value, "nova");
        Directory.CreateDirectory(workDir);
        var filePath = Path.Combine(workDir, "a.txt");
        File.WriteAllText(filePath, "v1");

        var factory = new FakeAgentHostFactory();
        var tracker = fixture.Services.GetRequiredService<FileChangeTracker>();
        var store = fixture.Services.GetRequiredService<FileStateStore>();
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory, tracker);
        await agentHost.StartAsync(ct);

        var (novaId, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var chatStore = fixture.Services.GetRequiredService<IChatStore>();
        var drafts = fixture.Services.GetRequiredService<Drafts>();
        var gateway = fixture.Services.GetRequiredService<IAgentGateway>();

        factory.Session.EnqueueReply("seed");
        await chat.PostAsync(roomId, KnownIds.Human, "hi", ct: ct);
        await WaitForHistoryCountAsync(chatStore, roomId, 2, ct);

        // A Turn the Human stops mid-reply, after an Edit tool call has already touched a.txt.
        var rawInput = RawInputFor(filePath);
        factory.Session.EnqueueToolActivity(
            new ToolCallStarted(factory.Session.SessionId, "c1", "Write", ToolKind.Edit, ToolCallStatus.Pending, rawInput),
            new ToolCallUpdated(factory.Session.SessionId, "c1", "Write", ToolKind.Edit, ToolCallStatus.Completed, null, rawInput));
        factory.Session.EnqueueDripFedReply(TimeSpan.FromSeconds(2), "should not be posted");

        File.WriteAllText(filePath, "v2 edited");

        await chat.PostAsync(roomId, KnownIds.Human, "edit it", ct: ct);

        await WaitForToolStatusAsync(drafts, roomId, ToolActivityStatus.Completed, ct);
        await gateway.StopTurnAsync(novaId, roomId, ct);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        FileState? state = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            state = store.Load("nova");
            if (state is not null
                && state.Rooms.TryGetValue(roomId, out var baseline)
                && baseline.Folders.TryGetValue("nova", out var snapshot)
                && snapshot.Files.TryGetValue("a.txt", out var entry)
                && entry.Size == new FileInfo(filePath).Length)
            {
                break;
            }

            await Task.Delay(50, ct);
        }

        Assert.NotNull(state);
        Assert.True(state.Rooms.TryGetValue(roomId, out var roomBaseline));
        Assert.True(roomBaseline.Folders.TryGetValue("nova", out var folderSnapshot));
        Assert.True(folderSnapshot.Files.TryGetValue("a.txt", out var fileEntry));
        Assert.Equal(new FileInfo(filePath).Length, fileEntry.Size);
    }

    /// <summary>FC §6.8 item 4: a failed commit never fails the Turn - the reply still posts, and the consumer loop keeps running for a third Turn.</summary>
    [Fact]
    public async Task CommitFails_TurnStillPostsAndConsumerLives()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();

        // Every save under FileStateStore starts with Directory.CreateDirectory(Folder), which throws
        // when Folder already exists as a file. Load tolerates this (File.Exists on a path through a
        // file, rather than a directory, simply returns false), so every Turn's collect still succeeds
        // and only the commit ever fails.
        var fileStatePath = Path.Combine(options.Value.DataDir, "file-state");
        File.WriteAllText(fileStatePath, "not a directory");

        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("one");
        factory.Session.EnqueueReply("two");
        factory.Session.EnqueueReply("three");
        var persona = new Persona("nova", "You are Nova.");
        var tracker = fixture.Services.GetRequiredService<FileChangeTracker>();

        await using var agentHost = CreateHost(fixture, persona, factory, tracker);
        await agentHost.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();

        await chat.PostAsync(roomId, KnownIds.Human, "one", ct: ct);
        await chat.PostAsync(roomId, KnownIds.Human, "two", ct: ct);
        await chat.PostAsync(roomId, KnownIds.Human, "three", ct: ct);

        var history = await WaitForHistoryCountAsync(store, roomId, 6, ct);
        Assert.Equal(6, history.Count);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (!fixture.LogEntries.Any(entry =>
                entry.Level == LogLevel.Warning
                && entry.Message.Contains("failed to commit File Changes", StringComparison.Ordinal))
            && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50, ct);
        }

        Assert.Contains(
            fixture.LogEntries,
            entry => entry.Level == LogLevel.Warning && entry.Message.Contains("failed to commit File Changes", StringComparison.Ordinal));
    }

    /// <summary>Finding P-13: a runner built with no tracker at all never collects or commits, whatever happens on disk.</summary>
    [Fact]
    public async Task NullTracker_ChangesNothing()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var workDir = OwnWorkDir(options.Value, "nova");
        Directory.CreateDirectory(workDir);
        var filePath = Path.Combine(workDir, "a.txt");
        File.WriteAllText(filePath, "v1");

        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("one");
        factory.Session.EnqueueReply("two");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory, fileChanges: null);
        await agentHost.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        var directory = fixture.Services.GetRequiredService<ITeamDirectory>();

        await chat.PostAsync(roomId, KnownIds.Human, "hi", ct: ct);
        await WaitForHistoryCountAsync(store, roomId, 2, ct);

        File.WriteAllText(filePath, "v1 changed");

        await chat.PostAsync(roomId, KnownIds.Human, "hi again", ct: ct);
        await WaitForHistoryCountAsync(store, roomId, 4, ct);

        var room = await directory.GetRoomAsync(roomId, ct);
        Assert.NotNull(room);
        var expected1 = RoomSession.BuildPrompt(new WorkItem(roomId, room.Name, "You", "hi", []), new FakePromptSource());
        var expected2 = RoomSession.BuildPrompt(new WorkItem(roomId, room.Name, "You", "hi again", []), new FakePromptSource());

        Assert.Equal(2, factory.Session.Prompts.Count);
        Assert.Equal(expected1, factory.Session.Prompts[0]);
        Assert.Equal(expected2, factory.Session.Prompts[1]);
    }

    /// <summary>Builds a <see cref="PersonaRunner"/> over <paramref name="fixture"/>'s real pipe, with the given (or absent) File Changes tracker.</summary>
    private static PersonaRunner CreateHost(
        PipeHostFixture fixture, Persona persona, FakeAgentHostFactory factory, FileChangeTracker? fileChanges)
    {
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();

        // The fixture's own logger, not NullLogger, so a Warning this runner logs (a failed collect
        // or commit) reaches fixture.LogEntries - CapturingLoggerProvider is wired into the fixture's
        // host, and NullLogger would silently discard every entry a test like
        // CommitFails_TurnStillPostsAndConsumerLives needs to assert against.
        var logger = fixture.Services.GetRequiredService<ILogger<PersonaRunner>>();
        return new PersonaRunner(persona, options, factory, new FakePromptSource(), new RoomFollows(), logger, fileChanges);
    }

    /// <summary><paramref name="agentName"/>'s own Work Dir, always the first Watched Folder (FC §6.7 step 1).</summary>
    private static string OwnWorkDir(TeamOptions options, string agentName) =>
        new TeammatePaths(Options.Create(options)).WorkDir(agentName);

    /// <summary>A minimal <c>file_path</c> raw-input JSON body naming <paramref name="fullPath"/>.</summary>
    private static string RawInputFor(string fullPath) =>
        JsonSerializer.Serialize(new Dictionary<string, string> { ["file_path"] = fullPath });

    /// <summary>Waits until <paramref name="drafts"/> reports <paramref name="status"/> for some Draft in <paramref name="roomId"/> - proof the event reader has already processed the tool-call event (and so already recorded its touched path) before the caller acts further, closing the race finding P-1's live check exists to guard against.</summary>
    private static async Task WaitForToolStatusAsync(Drafts drafts, string roomId, ToolActivityStatus status, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (!drafts.ForRoom(roomId).Any(draft => draft.ToolStatus == status) && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(20, ct);
        }

        Assert.Contains(drafts.ForRoom(roomId), draft => draft.ToolStatus == status);
    }

    /// <summary>Waits until <paramref name="agentName"/> is registered and Online, then returns its id and its direct Room with the Human.</summary>
    private static async Task<(string AgentId, string RoomId)> WaitForDirectRoomAsync(
        PipeHostFixture fixture, string agentName, CancellationToken ct)
    {
        var directory = fixture.Services.GetRequiredService<ITeamDirectory>();
        var gateway = fixture.Services.GetRequiredService<IAgentGateway>();

        while (true)
        {
            var user = await directory.FindUserByNameAsync(agentName, ct);
            if (user is not null && gateway.IsOnline(user.Id))
            {
                var room = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, user.Id, ct);
                if (room is not null)
                {
                    return (user.Id, room.Id);
                }
            }

            await Task.Delay(50, ct);
        }
    }

    /// <summary>Waits until <paramref name="roomId"/>'s Transcript holds at least <paramref name="count"/> Messages.</summary>
    private static async Task<IReadOnlyList<ChatMessage>> WaitForHistoryCountAsync(
        IChatStore store, string roomId, int count, CancellationToken ct)
    {
        while (true)
        {
            var history = await store.ReadAllAsync(roomId, ct);
            if (history.Count >= count)
            {
                return history;
            }

            await Task.Delay(50, ct);
        }
    }
}
