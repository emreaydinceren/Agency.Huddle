using Agency.Huddle.App.Components.Shared;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins <see cref="RendererDispatch.RunAsync(Func{Func{Task}, Task}, Func{Task})"/>: the action runs
/// through the supplied invoker, the two exceptions that mean the renderer is gone are swallowed, and
/// anything else still reaches the caller.
/// </summary>
public sealed class RendererDispatchTests
{
    /// <summary>The action is handed to the invoker, and the invoker's run of it is what executes it.</summary>
    [Fact]
    public async Task RunAsync_ValidAction_RunsItThroughTheInvoker()
    {
        int invokerCalls = 0;
        int actionRuns = 0;

        await RendererDispatch.RunAsync(
            async work =>
            {
                invokerCalls++;
                await work();
            },
            () =>
            {
                actionRuns++;
                return Task.CompletedTask;
            });

        Assert.Equal(1, invokerCalls);
        Assert.Equal(1, actionRuns);
    }

    /// <summary>A circuit that is already gone surfaces as <see cref="ObjectDisposedException"/>, which means there is nothing to repaint.</summary>
    [Fact]
    public async Task RunAsync_InvokerThrowsObjectDisposed_IsSwallowed()
    {
        Task run = RendererDispatch.RunAsync(
            static _ => throw new ObjectDisposedException("circuit"),
            static () => Task.CompletedTask);

        await run;

        Assert.True(run.IsCompletedSuccessfully);
    }

    /// <summary>A renderer disposed mid-dispatch surfaces as <see cref="InvalidOperationException"/>, which is likewise swallowed.</summary>
    [Fact]
    public async Task RunAsync_ActionThrowsInvalidOperation_IsSwallowed()
    {
        Task run = RendererDispatch.RunAsync(
            static work => work(),
            static () => throw new InvalidOperationException("renderer disposed"));

        await run;

        Assert.True(run.IsCompletedSuccessfully);
    }

    /// <summary>Any other exception is a real fault in the action, so it propagates instead of being hidden.</summary>
    [Fact]
    public async Task RunAsync_ActionThrowsOtherException_Propagates()
    {
        ArgumentException thrown = await Assert.ThrowsAsync<ArgumentException>(() => RendererDispatch.RunAsync(
            static work => work(),
            static () => throw new ArgumentException("real bug")));

        Assert.Equal("real bug", thrown.Message);
    }
}
