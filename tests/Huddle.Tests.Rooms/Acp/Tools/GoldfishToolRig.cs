using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Goldfish;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// The shared rig of <see cref="GoldfishToolTests"/> and <see cref="GoldfishDoneToolTests"/>: the Tasks harness (Nova, Kai, a
/// Room "Pricing"), a real <see cref="GoldfishStore"/> and <see cref="GoldfishSupervisor"/> over the harness's Chat service and
/// Turn Queue, and a recording runner link. Nothing here is a product class.
/// </summary>
internal sealed class GoldfishToolRig : IDisposable
{
    private GoldfishToolRig(TaskToolHarness harness, Room room, string novaId, string kaiId)
    {
        this.Harness = harness;
        this.Room = room;
        this.NovaId = novaId;
        this.KaiId = kaiId;
        this.Queue = new TurnQueueStore(harness.Options);
        harness.Chat.OwedTurns = new OwedTurnPolicy(new RoomFollows());
        harness.Chat.Ledger = new TurnQueue(this.Queue, harness.Options, NullLogger<TurnQueue>.Instance, new Agency.Huddle.Tests.Desktop.FakeLifetime(), harness.Clock);
        this.Store = new GoldfishStore(harness.Options);
        this.Supervisor = new GoldfishSupervisor(this.Store, harness.Directory, harness.Chat, this.Link, harness.Options, NullLogger<GoldfishSupervisor>.Instance, harness.Clock);
        this.Guard = new GoldfishGuard(this.Supervisor, harness.TurnActivity, this.Prompts);
    }

    public TaskToolHarness Harness { get; }

    public Room Room { get; }

    public string NovaId { get; }

    public string KaiId { get; }

    public TurnQueueStore Queue { get; }

    public GoldfishStore Store { get; }

    public RecordingLink Link { get; } = new();

    public FakePromptSource Prompts { get; } = new();

    public GoldfishSupervisor Supervisor { get; }

    public GoldfishGuard Guard { get; }

    public TurnActivity Activity => this.Harness.TurnActivity;

    /// <summary>Builds the rig; Nova and Kai are members of the Room "Pricing" with the Human.</summary>
    /// <param name="ct">Cancels the setup.</param>
    public static async Task<GoldfishToolRig> CreateAsync(CancellationToken ct)
    {
        TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        string novaId = harness.NovaId ?? throw new InvalidOperationException("No Nova.");
        string kaiId = harness.KaiId ?? throw new InvalidOperationException("No Kai.");
        Room room = await harness.Directory.CreateRoomAsync("Pricing", [KnownIds.Human, novaId, kaiId], ct);
        return new GoldfishToolRig(harness, room, novaId, kaiId);
    }

    /// <summary>The <c>goldfish</c> tool, called as Nova.</summary>
    public GoldfishTool Tool() =>
        new(this.NovaId, this.Supervisor, this.Supervisor, this.Activity, this.Harness.Directory, this.Harness.Service, this.Prompts, this.Harness.Options);

    /// <summary>The <c>goldfish_done</c> tool, called as Nova.</summary>
    public GoldfishDoneTool DoneTool() =>
        new(this.NovaId, this.Guard, this.Supervisor, this.Harness.Directory, this.Prompts);

    /// <summary>Marks Nova's calling Turn as running in the Room.</summary>
    public void NovaTurnInRoom() => this.Activity.Begin(this.NovaId, this.Room.Id);

    /// <summary>Inserts an open possession of Nova in the Room, in the given state.</summary>
    /// <param name="state">The state of the row.</param>
    public void OpenPossession(PossessionState state = PossessionState.Working)
    {
        Possession row = new("p1", this.NovaId, this.Room.Id, null, "Look into it", state, null, null, this.Harness.Clock.GetUtcNow(), null, 0, 0m, null, null);
        Assert.True(this.Store.TryInsert(row));
    }

    /// <summary>The Messages Huddle posted into the Room, oldest first.</summary>
    /// <param name="ct">Cancels the read.</param>
    public async Task<IReadOnlyList<ChatMessage>> HuddleLinesAsync(CancellationToken ct) =>
        [.. (await this.Harness.Options.CreateChatStore().ReadAllAsync(this.Room.Id, ct)).Where(message => string.Equals(message.SenderId, KnownIds.Huddle, StringComparison.Ordinal))];

    /// <inheritdoc />
    public void Dispose() => this.Harness.Dispose();

    /// <summary>A runner link that records what the supervisor sends and sends nothing.</summary>
    internal sealed class RecordingLink : IGoldfishRunnerLink
    {
        public List<(string AgentId, GoldfishBegin Begin)> Begins { get; } = [];

        public List<(string AgentId, string PossessionId, GoldfishExit Exit)> Ends { get; } = [];

        public Task SendEndAsync(string agentId, string possessionId, GoldfishExit exit, CancellationToken ct)
        {
            this.Ends.Add((agentId, possessionId, exit));
            return Task.CompletedTask;
        }

        public Task SendBeginAsync(string agentId, GoldfishBegin begin, CancellationToken ct)
        {
            this.Begins.Add((agentId, begin));
            return Task.CompletedTask;
        }
    }
}
