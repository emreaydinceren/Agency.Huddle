using ToolsFakeAgentGateway = Agency.Huddle.Tests.Acp.Tools.FakeAgentGateway;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins that both <c>FakeAgentGateway</c> fakes can be read while another thread writes them. Tests flip an Agent
/// online while <c>Eventually.DirectRoomAsync</c> polls <c>IsOnline</c> from the pool, and bUnit's
/// <c>WaitForAssertion</c> reads the recorded calls while a component handler records one, so an unguarded
/// <see cref="HashSet{T}"/> or <see cref="List{T}"/> threw (<c>NullReferenceException</c> in
/// <c>HashSet.Contains</c>, seen under a coverage run). Real threads released together by one gate, over many
/// rounds, as agents/Testing.md asks of a concurrency test.
/// </summary>
public sealed class FakeAgentGatewayConcurrencyTests
{
    private const int Rounds = 100;
    private const int WritesPerRound = 2000;

    /// <summary>The page-level fake answers <c>IsOnline</c> without throwing while another thread sets Agents online and offline.</summary>
    [Fact]
    public void UiFake_IsOnline_WhileAnotherThreadWrites_NeverThrows()
    {
        for (int round = 0; round < Rounds; round++)
        {
            FakeAgentGateway gateway = new();
            Exception? failure = RaceReadAgainstWrite(
                () =>
                {
                    for (int i = 0; i < WritesPerRound; i++)
                    {
                        string id = Id(i);
                        gateway.SetOnline(id);
                        if (i % 3 == 0)
                        {
                            gateway.SetOffline(id);
                        }
                    }
                },
                i => gateway.IsOnline(Id(i % WritesPerRound)));

            Assert.Null(failure);
        }
    }

    /// <summary>The page-level fake's recorded calls can be enumerated while another thread records more.</summary>
    [Fact]
    public void UiFake_RecordedCalls_WhileAnotherThreadRecords_NeverThrow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        for (int round = 0; round < Rounds; round++)
        {
            FakeAgentGateway gateway = new();
            Exception? failure = RaceReadAgainstWrite(
                () =>
                {
                    for (int i = 0; i < WritesPerRound; i++)
                    {
                        _ = gateway.StopTurnAsync(Id(i), "room", ct);
                        _ = gateway.StartAfreshAsync(Id(i), "room", ct);
                    }
                },
                _ =>
                {
                    // A torn read surfaces as a default entry whose RoomId is null, or as "Collection was modified".
                    Assert.All(gateway.StopTurnCalls, static call => Assert.Equal("room", call.RoomId));
                    Assert.All(gateway.StartAfreshCalls, static call => Assert.Equal("room", call.RoomId));
                });

            Assert.Null(failure);
            Assert.Equal(WritesPerRound, gateway.StopTurnCalls.Count);
            Assert.Equal(WritesPerRound, gateway.StartAfreshCalls.Count);
        }
    }

    /// <summary>The Tools fake answers <c>IsOnline</c> without throwing while another thread sets Agents online.</summary>
    [Fact]
    public void ToolsFake_IsOnline_WhileAnotherThreadWrites_NeverThrows()
    {
        for (int round = 0; round < Rounds; round++)
        {
            ToolsFakeAgentGateway gateway = new();
            Exception? failure = RaceReadAgainstWrite(
                () =>
                {
                    for (int i = 0; i < WritesPerRound; i++)
                    {
                        gateway.SetOnline(Id(i));
                    }
                },
                i => gateway.IsOnline(Id(i % WritesPerRound)));

            Assert.Null(failure);
            Assert.True(gateway.IsOnline(Id(WritesPerRound - 1)));
        }
    }

    /// <summary>A distinct Agent id per index, so the set keeps growing and resizing under the reader.</summary>
    private static string Id(int index) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"agent-{index}");

    /// <summary>
    /// Starts one writer and one reader thread behind a shared gate, releases both together, keeps the reader
    /// calling <paramref name="read"/> until the writer finishes, and returns the first exception either threw.
    /// </summary>
    /// <param name="write">The writer's whole run.</param>
    /// <param name="read">One read, given a running counter.</param>
    /// <returns>The first exception thrown on either thread, or <see langword="null"/>.</returns>
    private static Exception? RaceReadAgainstWrite(Action write, Action<int> read)
    {
        using ManualResetEventSlim gate = new(initialState: false);
        Exception? failure = null;
        int writerDone = 0;

        Thread writer = new(() =>
        {
            gate.Wait();
            try
            {
                write();
            }
            catch (Exception ex)
            {
                // Recorded for the assert on the test thread; a throw here would end the process instead.
                Interlocked.CompareExchange(ref failure, ex, null);
            }
            finally
            {
                Volatile.Write(ref writerDone, 1);
            }
        });
        Thread reader = new(() =>
        {
            gate.Wait();
            try
            {
                for (int i = 0; Volatile.Read(ref writerDone) == 0; i++)
                {
                    read(i);
                }
            }
            catch (Exception ex)
            {
                // Recorded for the assert on the test thread; a throw here would end the process instead.
                Interlocked.CompareExchange(ref failure, ex, null);
            }
        });

        writer.Start();
        reader.Start();
        gate.Set();
        writer.Join();
        reader.Join();
        return failure;
    }
}
