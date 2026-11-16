using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.NeedsYou;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.NeedsYou;
using Agency.Huddle.Tests.Tasks;
using IProofSource = Agency.Huddle.App.Tasks.HumanGate.IProofSource;
using Proof = Agency.Huddle.App.Tasks.HumanGate.Proof;
using ProofItem = Agency.Huddle.App.Tasks.HumanGate.ProofItem;
using ProofItemSource = Agency.Huddle.App.Teams.ProofItemSource;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Shared setup for the Needs You Task tests: a bUnit context holding both the Room side (a
/// <see cref="NeedsYouFixture"/>) and the Task side (a <see cref="TaskToolHarness"/> and a real
/// <see cref="NeedsYouTaskSource"/> over its store), plus the seeding of a waiting Task.
/// </summary>
internal static class NeedsYouTaskTestHost
{
    /// <summary>The time the first waiting Task entered Needs You.</summary>
    public const string FirstEntry = "2026-10-01T09:00:00Z";

    /// <summary>The time the second waiting Task entered Needs You.</summary>
    public const string SecondEntry = "2026-10-03T09:00:00Z";

    /// <summary>An <see cref="IProofSource"/> that answers every Task with a Proof of three met items out of four.</summary>
    internal sealed class ThreeOfFourProofSource : IProofSource
    {
        /// <inheritdoc/>
        public Proof For(TaskItem task) => new(
        [
            new ProofItem("Draft", true, null, null, ProofItemSource.Process),
            new ProofItem("Sources", true, null, null, ProofItemSource.Process),
            new ProofItem("Tone", true, null, null, ProofItemSource.Process),
            new ProofItem("citation check", false, "Ruth", "19 of 23", ProofItemSource.Process),
        ]);
    }

    /// <summary>Builds a context holding the services the card and the page inject, over one Task source.</summary>
    /// <param name="fixture">The Room side.</param>
    /// <param name="harness">The Task side.</param>
    /// <param name="source">The Task source the components read.</param>
    public static MudBunitContext NewContext(NeedsYouFixture fixture, TaskToolHarness harness, NeedsYouTaskSource source)
    {
        MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);
        ctx.Services.AddSingleton(fixture.Tracker);
        ctx.Services.AddSingleton<ITeamDirectory>(fixture.Team);
        ctx.Services.AddSingleton<Agency.Huddle.App.Teams.ITeamCatalog>(new Agency.Huddle.Tests.Teams.FakeTeamCatalog());
        ctx.Services.AddSingleton(fixture.Events);
        ctx.Services.AddSingleton<TimeProvider>(fixture.Clock);
        ctx.Services.AddSingleton(source);
        return ctx;
    }

    /// <summary>Seeds a gated Task in Needs You, entered at <paramref name="at"/>.</summary>
    /// <param name="harness">The harness whose store receives the Task.</param>
    /// <param name="id">The Task id.</param>
    /// <param name="title">The title.</param>
    /// <param name="at">When it entered Needs You, as an ISO timestamp.</param>
    public static TaskItem SeedWaiting(TaskToolHarness harness, string id, string title, string at) =>
        harness.SeedOnDisk(TestTasks.Make(
            id: id,
            title: title,
            status: TaskState.NeedsYou,
            description: "Body",
            changeLog: [TestTasks.Entry(at, "Ada", "status: Review → Needs You (Proof 3/4)")]) with { HumanGate = true });

    /// <summary>Opens a Room that waits on the Human, so the Room list has one row.</summary>
    /// <param name="fixture">The Room side.</param>
    /// <param name="ct">Cancellation.</param>
    public static async Task<Agency.Huddle.App.Data.Room> WaitingRoomAsync(NeedsYouFixture fixture, CancellationToken ct)
    {
        Agency.Huddle.App.Data.User nova = await fixture.AddAgentAsync("Nova", ct);
        Agency.Huddle.App.Data.Room room = await fixture.CreateRoomAsync("Backend", [nova], ct);
        _ = await fixture.Service.PostAsync(room.Id, nova.Id, "which database, @You?", ct: ct);
        return room;
    }
}
