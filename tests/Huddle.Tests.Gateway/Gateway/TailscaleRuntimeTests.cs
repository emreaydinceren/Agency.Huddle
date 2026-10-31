using Agency.Huddle.App.Gateway;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Gateway;

/// <summary><see cref="TailscaleRuntime"/>'s shutdown: the host may dispose it before it runs the stopping step.</summary>
public sealed class TailscaleRuntimeTests
{
    /// <summary>
    /// Disposing first and stopping afterwards, the order a <c>WebApplicationFactory</c> teardown can take, stops cleanly
    /// instead of throwing <see cref="ObjectDisposedException"/> from the disposed token source.
    /// </summary>
    [Fact]
    public async Task StoppingAsync_AfterDispose_DoesNotThrow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayAdminHarness harness = new();
        FakeTimeProvider clock = new();
        FakeTailscaleCli cli = new(clock);
        TailscaleRuntime runtime = new(
            harness.Running,
            new GatewayListener(),
            cli,
            new TailscaleServeClaim(cli, clock),
            new GatewayDynamicHosts(),
            NullLogger<TailscaleRuntime>.Instance);
        await runtime.StartedAsync(ct);

        runtime.Dispose();
        Exception? thrown = await Record.ExceptionAsync(() => runtime.StoppingAsync(ct));

        Assert.Null(thrown);
    }
}
