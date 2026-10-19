using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Waiting;

/// <summary>
/// Polling waits for tests. Every wait checks its condition once more after the deadline before failing,
/// measures time with <see cref="Stopwatch"/>, and turns test-token cancellation into an assertion that
/// names what never arrived rather than a bare <see cref="TaskCanceledException"/>.
/// </summary>
internal static class Eventually
{
    private static readonly TimeSpan defaultInterval = TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// Polls <paramref name="condition"/> until it holds. Fails with <see cref="Assert.Fail(string)"/>
    /// naming the condition when <paramref name="timeout"/> elapses or <paramref name="ct"/> fires first.
    /// </summary>
    /// <param name="condition">The state the test needs before it can go on.</param>
    /// <param name="ct">The test's own deadline.</param>
    /// <param name="timeout">How long to wait; <see langword="null"/> means only <paramref name="ct"/> bounds the wait.</param>
    /// <param name="interval">The poll interval; defaults to 10 ms.</param>
    /// <param name="describe">Extra state appended to the failure message.</param>
    /// <param name="conditionText">The source text of <paramref name="condition"/>, supplied by the compiler.</param>
    /// <returns>A task that completes when the condition holds.</returns>
    internal static async Task UntilAsync(
        Func<bool> condition,
        CancellationToken ct,
        TimeSpan? timeout = null,
        TimeSpan? interval = null,
        Func<string>? describe = null,
        [CallerArgumentExpression(nameof(condition))] string conditionText = "")
    {
        long start = Stopwatch.GetTimestamp();
        TimeSpan poll = interval ?? defaultInterval;

        while (!condition())
        {
            TimeSpan delay = poll;
            if (timeout is { } limit)
            {
                TimeSpan remaining = limit - Stopwatch.GetElapsedTime(start);
                if (remaining <= TimeSpan.Zero)
                {
                    Assert.Fail(Message(conditionText, "timeout", describe?.Invoke()));
                }

                delay = remaining < poll ? remaining : poll;
            }

            await DelayOrFailAsync(delay, conditionText, describe, ct);
        }
    }

    /// <summary>
    /// Polls <paramref name="probe"/> until <paramref name="done"/> accepts a value, and returns that value.
    /// </summary>
    /// <typeparam name="T">The probed value type.</typeparam>
    /// <param name="probe">Reads the current value.</param>
    /// <param name="done">Whether a value is the one the test needs.</param>
    /// <param name="ct">The test's own deadline.</param>
    /// <param name="timeout">How long to wait; <see langword="null"/> means only <paramref name="ct"/> bounds the wait.</param>
    /// <param name="interval">The poll interval; defaults to 10 ms.</param>
    /// <param name="onTick">Runs once after each poll whose value was not accepted (for example to advance a fake clock).</param>
    /// <param name="what">Names the awaited value in the failure message.</param>
    /// <returns>The first accepted value.</returns>
    internal static async Task<T> ValueAsync<T>(
        Func<CancellationToken, Task<T>> probe,
        Func<T, bool> done,
        CancellationToken ct,
        TimeSpan? timeout = null,
        TimeSpan? interval = null,
        Action? onTick = null,
        string? what = null)
    {
        long start = Stopwatch.GetTimestamp();
        TimeSpan poll = interval ?? defaultInterval;
        string label = what ?? typeof(T).Name;

        while (true)
        {
            T value = await ProbeOrFailAsync(probe, label, ct);
            if (done(value))
            {
                return value;
            }

            onTick?.Invoke();

            string last = string.Create(CultureInfo.InvariantCulture, $"last value: {value}");
            TimeSpan delay = poll;
            if (timeout is { } limit)
            {
                TimeSpan remaining = limit - Stopwatch.GetElapsedTime(start);
                if (remaining <= TimeSpan.Zero)
                {
                    Assert.Fail(Message(label, "timeout", last));
                }

                delay = remaining < poll ? remaining : poll;
            }

            await DelayOrFailAsync(delay, label, () => last, ct);
        }
    }

    /// <summary>
    /// Waits until <paramref name="roomId"/>'s Transcript holds at least <paramref name="count"/> Messages and
    /// returns the whole Transcript.
    /// </summary>
    /// <param name="store">The chat store to read.</param>
    /// <param name="roomId">The Room to read.</param>
    /// <param name="count">The Message count to wait for.</param>
    /// <param name="ct">The test's own deadline.</param>
    /// <param name="timeout">How long to wait; <see langword="null"/> means only <paramref name="ct"/> bounds the wait.</param>
    /// <param name="onTick">Runs after each poll that came up short, for example to advance a fake clock so a drip-fed reply can finish.</param>
    /// <returns>The Transcript once it holds at least <paramref name="count"/> Messages.</returns>
    internal static Task<IReadOnlyList<ChatMessage>> HistoryCountAsync(
        IChatStore store,
        string roomId,
        int count,
        CancellationToken ct,
        TimeSpan? timeout = null,
        Action? onTick = null)
    {
        return ValueAsync(
            probeCt => store.ReadAllAsync(roomId, probeCt),
            history => history.Count >= count,
            ct,
            timeout,
            onTick: onTick,
            what: $"history count >= {count} in room {roomId}");
    }

    /// <summary>
    /// Waits until <paramref name="agentName"/> is registered and online, then returns its id and its direct
    /// Room with the Human. The Team Directory and the Agent gateway come from <paramref name="services"/>.
    /// </summary>
    /// <param name="services">The running app's services, such as <c>PipeHostFixture.Services</c>.</param>
    /// <param name="agentName">The Agent's Name.</param>
    /// <param name="ct">The test's own deadline.</param>
    /// <param name="timeout">How long to wait; <see langword="null"/> means only <paramref name="ct"/> bounds the wait.</param>
    /// <returns>The Agent's id and the id of its direct Room with the Human.</returns>
    internal static Task<(string AgentId, string RoomId)> DirectRoomAsync(
        IServiceProvider services, string agentName, CancellationToken ct, TimeSpan? timeout = null)
    {
        return DirectRoomAsync(services, services.GetRequiredService<ITeamDirectory>(), agentName, ct, timeout);
    }

    /// <summary>
    /// As <see cref="DirectRoomAsync(IServiceProvider, string, CancellationToken, TimeSpan?)"/>, but looks the
    /// Agent and its Room up in <paramref name="directory"/>.
    /// </summary>
    /// <param name="services">The running app's services; supplies the Agent gateway.</param>
    /// <param name="directory">The Team Directory to look in.</param>
    /// <param name="agentName">The Agent's Name.</param>
    /// <param name="ct">The test's own deadline.</param>
    /// <param name="timeout">How long to wait; <see langword="null"/> means only <paramref name="ct"/> bounds the wait.</param>
    /// <returns>The Agent's id and the id of its direct Room with the Human.</returns>
    internal static async Task<(string AgentId, string RoomId)> DirectRoomAsync(
        IServiceProvider services, ITeamDirectory directory, string agentName, CancellationToken ct, TimeSpan? timeout = null)
    {
        IAgentGateway gateway = services.GetRequiredService<IAgentGateway>();

        (string AgentId, string RoomId)? found = await ValueAsync<(string AgentId, string RoomId)?>(
            async probeCt =>
            {
                User? user = await directory.FindUserByNameAsync(agentName, probeCt);
                if (user is null || !gateway.IsOnline(user.Id))
                {
                    return null;
                }

                Room? room = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, user.Id, probeCt);
                return room is null ? null : (user.Id, room.Id);
            },
            static candidate => candidate is not null,
            ct,
            timeout,
            what: $"direct room with {agentName}");

        return found.GetValueOrDefault();
    }

    /// <summary>Runs the probe, converting test-token cancellation into a named failure.</summary>
    private static async Task<T> ProbeOrFailAsync<T>(Func<CancellationToken, Task<T>> probe, string label, CancellationToken ct)
    {
        try
        {
            return await probe(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Assert.Fail(Message(label, "cancellation", null));
            throw;
        }
    }

    /// <summary>Waits one poll interval, converting test-token cancellation into a named failure.</summary>
    private static async Task DelayOrFailAsync(TimeSpan delay, string label, Func<string>? describe, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Assert.Fail(Message(label, "cancellation", describe?.Invoke()));
        }
    }

    /// <summary>Builds the failure text: the awaited condition, why the wait ended, and any extra state.</summary>
    private static string Message(string label, string reason, string? detail)
    {
        string text = $"Timed out waiting for: {label} ({reason})";
        return string.IsNullOrEmpty(detail) ? text : $"{text}. {detail}";
    }
}
