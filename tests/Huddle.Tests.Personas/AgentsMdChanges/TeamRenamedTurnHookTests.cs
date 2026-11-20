using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Acp.Sessions.TurnHooks;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.AgentsMdChanges;

/// <summary>
/// Pins <see cref="TeamRenamedTurnHook"/> (Team Rename Spec R6, R7, §6.2; corrections tr8): who hears which journal rows, once, what the
/// session's state carries, and the exact notice text. A real journal and chat store stand behind the hook, and a fake clock stands behind both.
/// </summary>
public sealed class TeamRenamedTurnHookTests : IDisposable
{
    private const string RoomId = "room-1";
    private const string TeamTail =
        "\nEarlier Messages and your own notes use the old name and old paths; use the new ones from now on, and " +
        "update your own memory notes if they name the old ";

    private static readonly DateTimeOffset Start = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TurnHookScope Scope = new("Vera", ReadsFiles: true, FileChangesOn: false, DeclaredWatches: []);

    private readonly TempDataDir dir = new();
    private readonly FakeTimeProvider clock = new(Start);
    private readonly TeamRenameStore renames;
    private readonly FileChatStore chat;
    private readonly ITurnHook hook;

    /// <summary>Opens a journal and a chat store in a temp folder, and a hook over them that reads the last three Messages as the Catch-up.</summary>
    public TeamRenamedTurnHookTests()
    {
        this.renames = new TeamRenameStore(this.dir.Options(), this.clock);
        this.chat = this.dir.CreateChatStore();
        this.hook = new TeamRenamedTurnHook(this.renames, this.chat, new FixedSettings(3), new FakePromptSource(), this.clock);
    }

    /// <inheritdoc />
    public void Dispose() => this.dir.Dispose();

    /// <summary>A rename heard by a resumed session arrives as the notice naming both names and both folders, once, and the carry is that row's id.</summary>
    [Fact]
    public async Task Resumed_HearsADoneRename_Once_AndCarriesItsId()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string? state = await this.FreshStateAsync(ct);
        this.At(TimeSpan.FromDays(1));
        _ = this.RenameTeam("Marketing", "Growth");
        this.At(TimeSpan.FromDays(2));
        state = await this.hook.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Resumed, state), ct);

        TurnAddition? first = await this.hook.BeforeTurnAsync(new TurnStarting(Scope, RoomId, state), ct);
        state = await this.hook.AfterTurnAsync(new TurnEnded(Scope, RoomId, state, []), first, ct);
        TurnAddition? second = await this.hook.BeforeTurnAsync(new TurnStarting(Scope, RoomId, state), ct);

        Assert.NotNull(first);
        Assert.Equal(TeamNotice("Marketing", "Growth"), first.Text);
        Assert.NotNull(second);
        Assert.Null(second.Text);
        Assert.Equal(TeamNotice("Probe", "Probe2"), await this.ProbeAsync(state, second, ct));
    }

    /// <summary>Two renames heard in one Turn are one block, in id order, and the carry is the highest id included.</summary>
    [Fact]
    public async Task Resumed_TwoRenames_OneBlockInIdOrder_CarryIsTheHighest()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string? state = await this.FreshStateAsync(ct);
        this.At(TimeSpan.FromDays(1));
        _ = this.RenameTeam("Marketing", "Growth");
        _ = this.RenameTeam("Research", "Insights");
        state = await this.hook.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Resumed, state), ct);

        TurnAddition? addition = await this.hook.BeforeTurnAsync(new TurnStarting(Scope, RoomId, state), ct);

        Assert.NotNull(addition);
        Assert.Equal(TeamNotice("Marketing", "Growth") + "\n\n" + TeamNotice("Research", "Insights"), addition.Text);
        Assert.Equal(TeamNotice("Probe", "Probe2"), await this.ProbeAsync(state, addition, ct));
    }

    /// <summary>A rename that completes between Before and After was not in the notice, so After does not mark it heard: the next Turn tells it.</summary>
    [Fact]
    public async Task RenameCompletedBetweenBeforeAndAfter_IsNotMarkedHeard()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string? state = await this.FreshStateAsync(ct);
        this.At(TimeSpan.FromDays(1));
        _ = this.RenameTeam("Marketing", "Growth");
        state = await this.hook.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Resumed, state), ct);
        TurnAddition? before = await this.hook.BeforeTurnAsync(new TurnStarting(Scope, RoomId, state), ct);
        _ = this.RenameTeam("Research", "Insights");

        state = await this.hook.AfterTurnAsync(new TurnEnded(Scope, RoomId, state, []), before, ct);
        TurnAddition? next = await this.hook.BeforeTurnAsync(new TurnStarting(Scope, RoomId, state), ct);

        Assert.NotNull(next);
        Assert.Equal(TeamNotice("Research", "Insights"), next.Text);
        Assert.Equal(TeamNotice("Probe", "Probe2"), await this.ProbeAsync(state, next, ct));
        Assert.Equal(TeamNotice("Marketing", "Growth"), before?.Text);
    }

    /// <summary>A fresh session stores a marker when it opens, so its first Turn (which knows the Room) can resolve what it has heard.</summary>
    [Fact]
    public async Task Opening_Fresh_StoresAMarker()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        string? state = await this.hook.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Fresh, null), ct);

        Assert.False(string.IsNullOrWhiteSpace(state));
    }

    /// <summary>A fresh session whose last Messages start before the rename hears it on its first Turn, and the carry is that row's id.</summary>
    [Fact]
    public async Task Fresh_CatchUpStartsBeforeTheRename_HearsIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await this.PostAsync(RoomId, ct, 1, 2, 3, 4, 5);
        this.At(TimeSpan.FromHours(6));
        _ = this.RenameTeam("Marketing", "Growth");
        this.At(TimeSpan.FromHours(7));
        string? state = await this.hook.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Fresh, null), ct);

        TurnAddition? addition = await this.hook.BeforeTurnAsync(new TurnStarting(Scope, RoomId, state), ct);

        Assert.NotNull(addition);
        Assert.Equal(TeamNotice("Marketing", "Growth"), addition.Text);
        Assert.Equal(TeamNotice("Probe", "Probe2"), await this.ProbeAsync(state, addition, ct));
    }

    /// <summary>
    /// A fresh session whose last three Messages all come after the rename hears nothing, even though older Messages in the Room predate it:
    /// only the Catch-up counts.
    /// </summary>
    [Fact]
    public async Task Fresh_CatchUpPostdatesTheRename_HearsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await this.PostAsync(RoomId, ct, 1, 2, 7, 8, 9);
        this.At(TimeSpan.FromHours(6));
        _ = this.RenameTeam("Marketing", "Growth");
        this.At(TimeSpan.FromHours(10));
        string? state = await this.hook.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Fresh, null), ct);

        TurnAddition? addition = await this.hook.BeforeTurnAsync(new TurnStarting(Scope, RoomId, state), ct);

        Assert.NotNull(addition);
        Assert.Null(addition.Text);
        Assert.Equal(TeamNotice("Probe", "Probe2"), await this.ProbeAsync(state, addition, ct));
    }

    /// <summary>A fresh session in an empty Room hears nothing and carries the newest Done id, so a later rename is the only one it hears.</summary>
    [Fact]
    public async Task Fresh_EmptyRoom_HearsNothing_CarriesTheNewestDoneId()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.At(TimeSpan.FromHours(1));
        _ = this.RenameTeam("Marketing", "Growth");
        _ = this.RenameTeam("Research", "Insights");
        string? state = await this.hook.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Fresh, null), ct);

        TurnAddition? addition = await this.hook.BeforeTurnAsync(new TurnStarting(Scope, "empty-room", state), ct);

        Assert.NotNull(addition);
        Assert.Null(addition.Text);
        Assert.Equal(TeamNotice("Probe", "Probe2"), await this.ProbeAsync(state, addition, "empty-room", ct));
    }

    /// <summary>A session opened before the feature has no state and hears only the Done rows of the last 14 days.</summary>
    [Fact]
    public async Task Resumed_NoState_HearsOnlyTheLastFourteenDays()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.At(TimeSpan.FromDays(1));
        _ = this.RenameTeam("Marketing", "Growth");
        this.At(TimeSpan.FromDays(20));
        _ = this.RenameTeam("Research", "Insights");
        this.At(TimeSpan.FromDays(30));
        string? state = await this.hook.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Resumed, null), ct);

        TurnAddition? addition = await this.hook.BeforeTurnAsync(new TurnStarting(Scope, RoomId, state), ct);

        Assert.NotNull(addition);
        Assert.Equal(TeamNotice("Research", "Insights"), addition.Text);
        Assert.Equal(TeamNotice("Probe", "Probe2"), await this.ProbeAsync(state, addition, ct));
    }

    /// <summary>A compaction does not change what the session has heard: the state comes back as it was, and a missing state stays missing.</summary>
    [Fact]
    public async Task Opening_Compacted_LeavesTheStateUnchanged()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.At(TimeSpan.FromDays(1));
        _ = this.RenameTeam("Marketing", "Growth");
        string? stored = await this.hook.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Resumed, null), ct);

        string? compacted = await this.hook.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Compacted, stored), ct);
        string? none = await this.hook.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Compacted, null), ct);

        Assert.Equal(stored, compacted);
        Assert.Null(none);
    }

    /// <summary>A Turn with nothing to tell still returns an addition with no text, so After saves the carry.</summary>
    [Fact]
    public async Task Before_NothingToTell_StillReturnsAnAdditionWithNullText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string? state = await this.FreshStateAsync(ct);

        TurnAddition? addition = await this.hook.BeforeTurnAsync(new TurnStarting(Scope, RoomId, state), ct);

        Assert.NotNull(addition);
        Assert.Null(addition.Text);
    }

    /// <summary>A Project rename is told with the Team-qualified Project name and the Project's folder.</summary>
    [Fact]
    public async Task Resumed_ProjectRename_HearsTheProjectNotice()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string? state = await this.FreshStateAsync(ct);
        this.At(TimeSpan.FromDays(1));
        long id = this.renames.Begin(TeamRenameKind.Project, "Marketing", "Launch", "Launch-2027");
        this.renames.Complete(id);
        state = await this.hook.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Resumed, state), ct);

        TurnAddition? addition = await this.hook.BeforeTurnAsync(new TurnStarting(Scope, RoomId, state), ct);

        Assert.NotNull(addition);
        Assert.Equal(
            "Project 'Marketing/Launch' is now called 'Marketing/Launch-2027'. Its folder moved from `Teams/Marketing/Launch` to `Teams/Marketing/Launch-2027`." +
            TeamTail + "Project.",
            addition.Text);
    }

    /// <summary>The hook applies to every session, whatever the Persona reads or watches (Spec §6.2).</summary>
    [Fact]
    public void AppliesTo_EveryScope()
    {
        TurnHookScope bare = new("Vera", ReadsFiles: false, FileChangesOn: false, DeclaredWatches: []);

        Assert.True(this.hook.AppliesTo(bare));
        Assert.True(this.hook.AppliesTo(Scope));
    }

    /// <summary>The Team Rename hook runs first in production, before AGENTS.md, so the Agent learns where files moved before it reads about them.</summary>
    [Fact]
    public void Pipeline_ProductionOrder_HasTheRenameHookFirst()
    {
        ITurnHook[] reversed =
        [
            new NamedHook("FileChanges"),
            new NamedHook("HuddleMemory"),
            new NamedHook("AgentsMd"),
            new NamedHook(TeamRenamedTurnHook.HookName),
        ];

        TurnHookPipeline pipeline = TurnHookPipeline.ForPersona(Scope, reversed, NullLogger.Instance);

        Assert.Equal([TeamRenamedTurnHook.HookName, "AgentsMd", "HuddleMemory", "FileChanges"], pipeline.HookNames);
    }

    /// <summary>The two Team Room lines are Prompts with the Spec §6.4 wording (tr10 posts them).</summary>
    [Fact]
    public void RoomLinePrompts_HaveTheSpecWording()
    {
        Assert.Equal("This Team was renamed from **{{oldName}}** to **{{newName}}**.", PromptCatalog.Get("room.teamRenamed").Default);
        Assert.Equal("Project **{{oldProject}}** was renamed to **{{newProject}}**.", PromptCatalog.Get("room.projectRenamed").Default);
    }

    private static string TeamNotice(string oldName, string newName) =>
        $"Team '{oldName}' is now called '{newName}'. Its folder moved from `Teams/{oldName}` to `Teams/{newName}`." + TeamTail + "Team.";

    /// <summary>
    /// Pins what a Turn's carry became without naming its type: ends the Turn, journals one more rename, and returns what the next
    /// Turn tells. A carry too low would tell the earlier rows again; one too high would swallow the probe.
    /// </summary>
    private Task<string?> ProbeAsync(string? state, TurnAddition? addition, CancellationToken ct) =>
        this.ProbeAsync(state, addition, RoomId, ct);

    private async Task<string?> ProbeAsync(string? state, TurnAddition? addition, string roomId, CancellationToken ct)
    {
        string? stored = await this.hook.AfterTurnAsync(new TurnEnded(Scope, roomId, state, []), addition, ct);
        this.clock.Advance(TimeSpan.FromMinutes(1));
        _ = this.RenameTeam("Probe", "Probe2");
        stored = await this.hook.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Resumed, stored), ct);
        TurnAddition? next = await this.hook.BeforeTurnAsync(new TurnStarting(Scope, roomId, stored), ct);
        return next?.Text;
    }

    private void At(TimeSpan offset) => this.clock.SetUtcNow(Start + offset);

    private long RenameTeam(string oldName, string newName)
    {
        long id = this.renames.Begin(TeamRenameKind.Team, oldName, oldName, newName);
        this.renames.Complete(id);
        return id;
    }

    /// <summary>Opens a fresh session in an empty Room with nothing renamed, runs its first Turn and After, and returns the state it stores.</summary>
    private async Task<string?> FreshStateAsync(CancellationToken ct)
    {
        string? state = await this.hook.OnSessionOpeningAsync(new SessionOpening(Scope, SessionOpenReason.Fresh, null), ct);
        TurnAddition? first = await this.hook.BeforeTurnAsync(new TurnStarting(Scope, RoomId, state), ct);
        return await this.hook.AfterTurnAsync(new TurnEnded(Scope, RoomId, state, []), first, ct);
    }

    /// <summary>Posts one Message per number, each stamped that many hours after the start.</summary>
    private async Task PostAsync(string roomId, CancellationToken ct, params int[] hours)
    {
        foreach (int hour in hours)
        {
            ChatMessage message = new($"m{hour}", Start + TimeSpan.FromHours(hour), "You", "You", $"Message {hour}");
            await this.chat.AppendAsync(roomId, message, ct);
        }
    }

    /// <summary>Settings that read a fixed number of Messages.</summary>
    private sealed class FixedSettings(int messages) : ISessionSettings
    {
        public SessionSettings Current => new(messages, 100_000);
    }

    /// <summary>A hook that only has a name, to pin the order the pipeline sorts hooks into.</summary>
    private sealed class NamedHook(string name) : ITurnHook
    {
        public string Name => name;

        public bool AppliesTo(TurnHookScope scope) => true;

        public Task<string?> OnSessionOpeningAsync(SessionOpening opening, CancellationToken cancellationToken) => Task.FromResult<string?>(null);

        public Task<TurnAddition?> BeforeTurnAsync(TurnStarting turn, CancellationToken cancellationToken) => Task.FromResult<TurnAddition?>(null);

        public Task<string?> AfterTurnAsync(TurnEnded turn, TurnAddition? addition, CancellationToken cancellationToken) => Task.FromResult(turn.State);
    }
}
