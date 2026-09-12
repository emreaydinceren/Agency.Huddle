namespace Agency.Huddle.Acp.Tests.Fakes;

using System.IO;
using System.Threading.Tasks;
using Agency.Huddle.Acp.Hosting;

/// <summary>An <see cref="IAgentProcess"/> backed by one end of a duplex in-memory stream pair.</summary>
internal sealed class FakeAgentProcess : IAgentProcess
{
    private readonly Stream duplex;

    internal FakeAgentProcess(Stream duplex)
    {
        this.duplex = duplex;
    }

    public Stream StandardInput => this.duplex;

    public Stream StandardOutput => this.duplex;

    internal TaskCompletionSource<int> ExitSource { get; } = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<int> Exited => this.ExitSource.Task;

    internal bool Killed { get; private set; }

    public void Kill()
    {
        this.Killed = true;
        this.ExitSource.TrySetResult(-1);
    }

    public void Dispose()
    {
    }
}
