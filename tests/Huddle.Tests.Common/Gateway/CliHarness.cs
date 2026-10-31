using Agency.Huddle.App.Cli;
using Agency.Huddle.App.Desktop;
using Agency.Huddle.Tests.Desktop;
using Microsoft.Extensions.Configuration;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>A temporary data directory, fake console, recording browser opener and fake environment, wired into a <see cref="CliContext"/>.</summary>
internal sealed class CliHarness : IDisposable
{
    /// <summary>The gateway token the configuration supplies unless <c>configToken</c> is off.</summary>
    public const string Token = "0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>The instance token a "running" copy publishes.</summary>
    public const string InstanceToken = "instance-token-xyz";

    private readonly TempDataDir dir = new();
    private InstanceLock? runningLock;

    /// <summary>Gets the data directory path.</summary>
    public string DataDir => this.dir.Path;

    /// <summary>Gets the fake console.</summary>
    public FakeCliConsole Console { get; } = new();

    /// <summary>Gets the recording opener.</summary>
    public RecordingBrowserOpener Opener { get; } = new();

    /// <summary>Gets the fake environment.</summary>
    public FakeGatewayEnvironment Environment { get; } = new();

    /// <summary>Gets or sets whether the configuration carries <see cref="Token"/> as <c>Team:Gateway:Auth:Token</c>.</summary>
    public bool ConfigToken { get; set; } = true;

    /// <summary>Publishes an <c>instance.json</c> and holds the instance lock, as a running desktop copy does.</summary>
    /// <param name="url">The copy's address.</param>
    public void StartRunningCopy(string url = "http://127.0.0.1:5123")
    {
        InstanceFile.Write(this.DataDir, new InstanceInfo(url, InstanceToken, 4242));
        this.runningLock = InstanceLock.TryAcquire(this.DataDir);
        Assert.NotNull(this.runningLock);
    }

    /// <summary>Publishes an <c>instance.json</c> without holding the lock: a stale record a crashed copy left.</summary>
    /// <param name="url">The recorded address.</param>
    public void WriteStaleRecord(string url = "http://127.0.0.1:5123")
        => InstanceFile.Write(this.DataDir, new InstanceInfo(url, InstanceToken, 4242));

    /// <summary>Builds the context.</summary>
    /// <param name="handler">The HTTP handler.</param>
    public CliContext Context(HttpMessageHandler? handler = null)
    {
        Dictionary<string, string?> values = [];
        if (this.ConfigToken)
        {
            values["Team:Gateway:Auth:Token"] = Token;
        }

        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new CliContext(this.DataDir, this.Console, this.Opener, configuration, this.Environment, handler);
    }

    /// <summary>Runs a verb and returns its exit code.</summary>
    /// <param name="handler">The HTTP handler.</param>
    /// <param name="ct">Cancellation.</param>
    /// <param name="args">The arguments.</param>
    public Task<int> RunAsync(HttpMessageHandler? handler, CancellationToken ct, params string[] args)
        => CliVerbs.RunAsync(args, this.Context(handler), ct);

    /// <inheritdoc />
    public void Dispose()
    {
        this.runningLock?.Dispose();
        this.Console.Dispose();
        this.dir.Dispose();
    }
}
