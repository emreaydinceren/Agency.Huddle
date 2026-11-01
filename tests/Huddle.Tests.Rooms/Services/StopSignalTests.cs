using Agency.Huddle.App.Services;

namespace Agency.Huddle.Tests.Services;

public sealed class StopSignalTests
{
    /// <summary>Stop cancels the token and reports stopped.</summary>
    [Fact]
    public void Stop_CancelsToken()
    {
        using StopSignal signal = new();

        signal.Stop();

        Assert.True(signal.Token.IsCancellationRequested);
        Assert.True(signal.IsStopped);
    }

    /// <summary>A Stop that runs after Dispose, as the host's stop thread can, neither throws nor un-cancels the token.</summary>
    [Fact]
    public void Stop_AfterDispose_DoesNotThrow()
    {
        StopSignal signal = new();
        signal.Dispose();

        signal.Stop();

        Assert.True(signal.IsStopped);
    }

    /// <summary>Dispose alone cancels the token, so work that only ever sees Dispose still ends.</summary>
    [Fact]
    public void Dispose_WithoutStop_CancelsToken()
    {
        StopSignal signal = new();
        CancellationToken token = signal.Token;

        signal.Dispose();

        Assert.True(token.IsCancellationRequested);
    }

    /// <summary>The token stays readable after Dispose, where a disposed source's Token property throws.</summary>
    [Fact]
    public void Token_AfterDispose_IsReadable()
    {
        StopSignal signal = new();
        signal.Dispose();

        CancellationToken token = signal.Token;

        Assert.True(token.IsCancellationRequested);
    }

    /// <summary>Dispose twice is a no-op the second time.</summary>
    [Fact]
    public void Dispose_Twice_DoesNotThrow()
    {
        StopSignal signal = new();
        signal.Dispose();

        signal.Dispose();

        Assert.True(signal.IsStopped);
    }

    /// <summary>Stop and Dispose released together on real threads never throw, in either order, over many rounds.</summary>
    [Fact]
    public void StopAndDispose_Concurrent_NeverThrow()
    {
        List<Exception> failures = [];
        Lock failuresGate = new();
        for (int round = 0; round < 200; round++)
        {
            StopSignal signal = new();
            using ManualResetEventSlim go = new();
            Thread stopper = new(() => Run(signal.Stop));
            Thread disposer = new(() => Run(signal.Dispose));
            stopper.Start();
            disposer.Start();
            go.Set();
            stopper.Join();
            disposer.Join();

            void Run(Action action)
            {
                go.Wait();
                try
                {
                    action();
                }
                catch (ObjectDisposedException ex)
                {
                    lock (failuresGate)
                    {
                        failures.Add(ex);
                    }
                }
            }
        }

        Assert.Empty(failures);
    }
}
