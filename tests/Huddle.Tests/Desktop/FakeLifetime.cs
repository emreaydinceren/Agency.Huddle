using Microsoft.Extensions.Hosting;

namespace Agency.Huddle.Tests.Desktop;

/// <summary>
/// An <see cref="IHostApplicationLifetime"/> whose three lifecycle tokens are fired by the test.
/// It owns three <see cref="CancellationTokenSource"/> instances, so it is disposable.
/// </summary>
internal sealed class FakeLifetime : IHostApplicationLifetime, IDisposable
{
    private readonly CancellationTokenSource started = new();

    private readonly CancellationTokenSource stopping = new();

    private readonly CancellationTokenSource stopped = new();

    /// <inheritdoc />
    public CancellationToken ApplicationStarted => this.started.Token;

    /// <inheritdoc />
    public CancellationToken ApplicationStopping => this.stopping.Token;

    /// <inheritdoc />
    public CancellationToken ApplicationStopped => this.stopped.Token;

    /// <summary>Gets a value indicating whether <see cref="StopApplication"/> has been called.</summary>
    public bool StopRequested => this.stopping.IsCancellationRequested;

    /// <summary>Fires <see cref="ApplicationStarted"/>, running its registered callbacks on the calling thread.</summary>
    public void FireStarted()
    {
        this.started.Cancel();
    }

    /// <inheritdoc />
    public void StopApplication()
    {
        this.stopping.Cancel();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.started.Dispose();
        this.stopping.Dispose();
        this.stopped.Dispose();
    }
}
