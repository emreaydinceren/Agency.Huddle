using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Data;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions;
using Agency.Huddle.Tests.Acp.Sessions.Fakes;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Waiting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Pins how a goldfish possession's Spend finds its Task (Goldfish corrections slice 3, step 6): a clean session passes the possession's Task id
/// with every cost it reports, a warm session passes none, and the runner writes the id into <c>persona_spend.task_id</c>.
/// </summary>
public sealed class GoldfishSpendTests
{
    private static readonly Persona Nova = new("nova", "You are Nova.", Model: "m1", Effort: "e1");

    /// <summary>A clean session's cost reaches the owner with the possession's Task id.</summary>
    [Fact]
    public async Task GoldfishSession_PassesTheTaskIdWithItsSpend()
    {
        FakeRoomSessionOwner owner = await RunOneTurnAsync(new CleanSessionSpec("p1", "room-1", "task-9"), clean: true);

        Assert.Equal("task-9", Assert.Single(owner.SpendTaskIds));
        Assert.Equal("room-1", Assert.Single(owner.SpendRoomIds));
    }

    /// <summary>A possession with no Task passes none: the Spend is recorded as it always was.</summary>
    [Fact]
    public async Task GoldfishSession_WithNoTask_PassesNone()
    {
        FakeRoomSessionOwner owner = await RunOneTurnAsync(new CleanSessionSpec("p1", "room-1", null), clean: true);

        Assert.Null(Assert.Single(owner.SpendTaskIds));
        Assert.Equal(0.05m, Assert.Single(owner.SpendAdded).RunningTotal);
    }

    /// <summary>A warm session passes no Task id, the control that proves the first row is about the possession and not every session.</summary>
    [Fact]
    public async Task WarmSession_PassesNoTaskId()
    {
        FakeRoomSessionOwner owner = await RunOneTurnAsync(goldfish: null, clean: false);

        Assert.Null(Assert.Single(owner.SpendTaskIds));
        Assert.Equal(0.05m, Assert.Single(owner.SpendAdded).RunningTotal);
    }

    /// <summary>The runner turns the Task id into the Spend row's Task: a cost with an id lands under it, a cost without lands under the empty id, and the two never merge.</summary>
    [Fact]
    public async Task RunnerAddSpend_WritesTheTaskIdIntoPersonaSpend()
    {
        using TempDataDir dir = new();
        PersonaSpend spend = new(NullLogger<PersonaSpend>.Instance, new PersonaSpendStore(dir.Options()), new RoomSpendStore(dir.Options()));
        await using PersonaRunner runner = new(
            TestPersonas.Nova,
            TestOptions.Create(static opts => opts.PipeName = "goldfish-spend-unused"),
            new FakeAgentHostFactory(),
            new FakePromptSource(),
            new RoomFollows(),
            NullLogger<PersonaRunner>.Instance,
            spend: spend);
        IRoomSessionOwner owner = runner;

        owner.AddSpend("goldfish-session", 0.50m, "USD", "room-1", "task-9");
        owner.AddSpend("warm-session", 0.25m, "USD", "room-1");

        Assert.Equal([("", 0.25m), ("task-9", 0.50m)], PersonaSpendRows(dir, "nova"));
    }

    private static async Task<FakeRoomSessionOwner> RunOneTurnAsync(CleanSessionSpec? goldfish, bool clean)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReplyWithUsageAndCost([100], [new UsageCost(0.05m, "USD")], "done");
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = RoomSessionHarness.Create(
            owner,
            RoomSessionHarness.FixedOpen(session),
            new RoomSessionHarnessOptions { Persona = Nova, AgentId = "agent-1", Clean = clean, Goldfish = goldfish });
        WorkItemKind kind = goldfish is null ? WorkItemKind.Message : WorkItemKind.GoldfishBrief;

        harness.Room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", "Human", "do it", [], kind, TriggerMessageId: "trig-1")));
        await Eventually.UntilAsync(() => owner.SpendAdded.Count == 1, ct, timeout: TestBudgets.Standard);
        return owner;
    }

    private static List<(string TaskId, decimal Amount)> PersonaSpendRows(TempDataDir dir, string personaName)
    {
        using SqliteConnection connection = new($"Data Source={System.IO.Path.Combine(dir.Path, "team.db")};Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT task_id, amount FROM persona_spend WHERE persona_name = $name ORDER BY task_id;";
        command.Parameters.AddWithValue("$name", personaName);
        using SqliteDataReader reader = command.ExecuteReader();
        List<(string, decimal)> rows = [];
        while (reader.Read())
        {
            rows.Add((reader.GetString(0), reader.GetDecimal(1)));
        }

        return rows;
    }
}
