using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions.Fakes;
using Agency.Huddle.Tests.Waiting;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// Pins what closing the goldfish slot under a running Turn leaves behind: the Turn's Draft is ended the way a Human's Stop ends it, and the
/// close is not counted as the Persona failing a Turn (Goldfish manual-run bug B3).
/// </summary>
public sealed class GoldfishSlotCloseTests
{
    private static readonly Persona Nova = new("nova", "You are Nova.", Model: "m1", Effort: "e1");

    private static readonly CleanSessionSpec Spec = new("p1", "A", "t1");

    /// <summary>Closing the slot while the goldfish's Turn is open (it called goldfish_done inside it) writes that Turn's Draft terminator and reports no failure.</summary>
    [Fact]
    public async Task CloseGoldfishAsync_TurnInFlight_EndsItsDraftAndReportsNoFailure()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CancellationTokenSource runCts = new();
        FakeAgentSession warm = new();
        FakePersonaHost host = new(warm, Profile()) { CanResume = true };
        host.OnOpen = session =>
        {
            if (!ReferenceEquals(session, warm))
            {
                session.EnqueueReplyThenHang("working");
            }
        };
        FakeRoomSessionOwner owner = new();
        await using RoomSessionPool pool = new(host, owner, Nova, new FakePromptSource(), new AcpOptions(), TimeProvider.System, NullLogger.Instance, runCts.Token, agentId: "agent-1");
        try
        {
            pool.OpenGoldfish("A", Spec);
            pool.EnqueueGoldfish(new QueuedWork(1, new WorkItem("A", "A", "Human", "the brief", [], TriggerMessageId: "trig-1")));
            await Eventually.UntilAsync(() => owner.Written.OfType<MessageDelta>().Any(delta => delta.Text.Length > 0), ct, timeout: TestBudgets.Standard);

            await pool.CloseGoldfishAsync().WaitAsync(TestBudgets.Standard, ct);

            string messageId = owner.Written.OfType<MessageDelta>().First().MessageId;
            Assert.Equal(
                (1, 0),
                (owner.Written.OfType<MessageDelta>().Count(delta => delta.IsFinal && delta.MessageId == messageId), owner.ReportCalls.Count(call => call == nameof(FakeRoomSessionOwner.ReportTurnFailure))));
        }
        finally
        {
            await runCts.CancelAsync();
        }
    }

    private static AdapterProfile Profile() => new(
        Id: "claude",
        DisplayName: "Claude",
        Description: null,
        Command: "claude-agent-acp",
        Args: null,
        AdapterPath: null,
        UsesToolNamePrefix: true,
        EnvironmentOverrides: null,
        ReadsFiles: true,
        IsolateUserSettings: true,
        SessionPerRoom: true);
}
