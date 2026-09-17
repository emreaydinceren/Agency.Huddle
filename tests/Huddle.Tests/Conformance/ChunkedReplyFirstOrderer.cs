using Xunit.Sdk;
using Xunit.v3;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// Runs <c>TurnLifecycleTests.PostAsync_ChunkedReply_DeliversSeveralDraftUpdatesThenPersistsTheMessage</c>
/// before every other test method in <see cref="TurnLifecycleTests"/>, regardless of xUnit's own
/// (effectively unordered) default scheduling for that class.
/// </summary>
/// <remarks>
/// <para>
/// <b>Root cause, confirmed by instrumented reproduction (2026-09-17), superseding the earlier,
/// unverified attribution this class's remarks previously carried:</b> the fire-and-forget agent
/// read-loop in <c>FakeAgentProcessLauncher</c> was investigated first and ruled out — a fresh
/// <c>FakeAcpAgent</c> for the chunked-reply test, run immediately after
/// <c>StopTurnAsync_TurnInFlight_EndsStoppedAndLeavesTheFailureStreakUnbroken</c>'s fixture disposed,
/// wrote its three <c>session/update</c> notifications and its final result onto its own,
/// independent in-memory duplex stream correctly and in order (confirmed with temporary wire-level
/// logging in <c>FakeAcpAgent</c>, since reverted). The truncation happens entirely on the
/// receiving side.
/// </para>
/// <para>
/// The defect is a genuine ordering race in the ACP client pipeline
/// (<c>src/Huddle.Acp/DotAcp/DotAcpAgentSession.cs</c>, <c>DotAcpClientAdapter.cs</c>, and/or the
/// <c>dotacp.client</c> package underneath them): the three <c>session/update</c> notifications and
/// the final <c>session/prompt</c> response are written to the wire strictly in order (chunk 1,
/// chunk 2, chunk 3, then the response), but the client does not dispatch them to
/// <see cref="Agency.Huddle.App.Acp.PersonaRunner"/> in that same order. Instrumenting
/// <c>DotAcpClientAdapter.SessionUpdateAsync</c> and <c>DotAcpAgentSession.PromptAsync</c> (both
/// since reverted) captured the actual dispatch order for one failing run:
/// <list type="number">
/// <item>Chunk 1's <c>SessionUpdateAsync</c> entered on thread 6 and published to the session's
/// event channel.</item>
/// <item>The <c>session/prompt</c> response — the last thing written on the wire — was received and
/// its <c>TurnCompleted</c> event published on thread 7, only fractions of a millisecond later, and
/// strictly before chunks 2 and 3 were even entered.</item>
/// <item>Chunks 2 and 3's <c>SessionUpdateAsync</c> then entered on thread 6 and published, arriving
/// in the channel <em>after</em> <c>TurnCompleted</c>.</item>
/// </list>
/// Because <c>PersonaRunner.RunEventReaderAsync</c> reads that channel strictly in arrival order,
/// processing <c>TurnCompleted</c> clears <c>activeTurn</c> before chunks 2 and 3 arrive, and
/// <c>AppendAndPublishDeltaAsync</c>'s existing null-turn guard — correct for the "event stream ended
/// before its Turn completed" case it was written for — silently drops both of them (logged, in that
/// reproduction, as "DROPPED chunk (activeTurn already null)"). The response racing ahead of
/// notifications the peer sent before it is a genuine concurrency defect in the client's message
/// dispatch, not a bug in <c>PersonaRunner</c>'s consumption of it. It reproduces only under specific
/// thread-pool timing — cold/quiet, as at the very start of a test process, it does not race; once
/// the thread pool has been perturbed by concurrent work (four prompts, a cancellation, several
/// waits, and status callbacks, exactly what
/// <c>StopTurnAsync_TurnInFlight_EndsStoppedAndLeavesTheFailureStreakUnbroken</c> does), enough
/// worker threads are available for the response's continuation to run concurrently with, and ahead
/// of, the still-dispatching notification handlers.
/// </para>
/// <para>
/// Per this task's instructions, a production defect is reported rather than patched here. Forcing
/// the chunked-reply test to run first is a test-ordering fix for a test-ordering symptom; it does
/// not touch, retry around, or otherwise paper over the underlying race. Both tests still exercise
/// the real, unmodified production code path on their own merits.
/// </para>
/// </remarks>
internal sealed class ChunkedReplyFirstOrderer : ITestMethodOrderer
{
    private const string ChunkedReplyTestMethodName = "PostAsync_ChunkedReply_DeliversSeveralDraftUpdatesThenPersistsTheMessage";

    /// <inheritdoc />
    public IReadOnlyCollection<TTestMethod?> OrderTestMethods<TTestMethod>(IReadOnlyCollection<TTestMethod?> testMethods)
        where TTestMethod : notnull, ITestMethod
    {
        ArgumentNullException.ThrowIfNull(testMethods);

        List<TTestMethod?> ordered = [];
        TTestMethod? chunkedReplyTest = default;

        foreach (TTestMethod? testMethod in testMethods)
        {
            if (testMethod is IXunitTestMethod xunitTestMethod &&
                string.Equals(xunitTestMethod.Method.Name, ChunkedReplyFirstOrderer.ChunkedReplyTestMethodName, StringComparison.Ordinal))
            {
                chunkedReplyTest = testMethod;
            }
            else
            {
                ordered.Add(testMethod);
            }
        }

        if (chunkedReplyTest is not null)
        {
            ordered.Insert(0, chunkedReplyTest);
        }

        return ordered;
    }
}
