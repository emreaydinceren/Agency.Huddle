using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Pipes;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// Task 10.6 (Spec §15.8, T-30): the one conformance test that does not substitute
/// <see cref="IAgentProcessLauncher"/>. Every other test in this folder drives a Persona against
/// <see cref="Agency.Huddle.Acp.Tests.Fakes.FakeAcpAgent"/> over an in-proc duplex stream pair via
/// <see cref="MockAdapterFixture"/> (Spec §6.10, "Two modes"), which proves everything except the
/// launch itself. This test instead configures an Adapter Profile whose <c>Command</c> is the real
/// <c>mock-acp</c> executable this solution's own build produced, builds a real Huddle host with the
/// real, unsubstituted <see cref="AgentProcessLauncher"/> that
/// <see cref="ServiceCollectionExtensions.AddTeamServices"/> registers by default, and drives one
/// Persona through one real Turn against that real child process end to end:
/// <see cref="AgentProcessOptionsFactory"/> resolves the launch options,
/// <see cref="AgentProcessLauncher"/> spawns <c>mock-acp</c>, and <c>DotAcpAgentHost</c> speaks ACP
/// to it over its stdin/stdout.
/// </summary>
/// <remarks>
/// <para>
/// Per Spec §6.10 and Task 10.6, this is deliberately the only process-mode test in the suite; every
/// other conformance test uses <see cref="MockAdapterFixture"/>'s in-proc duplex stream instead. The
/// executable is located the same way <c>MockAdapterTests.MockAdapterExecutablePath</c> already does:
/// walk up from <see cref="AppContext.BaseDirectory"/> to the directory containing
/// <c>Huddle.slnx</c>, then read the configuration and target-framework segments off this test
/// assembly's own output path, so the test follows whichever configuration it was itself built under
/// (Debug or Release) rather than hard-coding one.
/// </para>
/// <para>
/// The assertion is deliberately limited to "the Turn completed" — a reply from the Persona landed in
/// the Room — never the exact chunk count or reply text. <c>ChunkedReplyFirstOrderer</c>'s remarks
/// document a known, diagnosed race in the ACP client's event dispatch
/// (<c>DotAcpAgentSession</c>/<c>DotAcpClientAdapter</c>) that can drop trailing
/// <c>MessageDelta</c> chunks when a <c>session/prompt</c> response is dispatched ahead of
/// <c>session/update</c> notifications the peer sent before it; pinning chunk counts or full text
/// here would make this test intermittently red for a defect that belongs to that class, not this
/// one.
/// </para>
/// </remarks>
public sealed class ProcessModeTests
{
    /// <summary>
    /// A Persona on an Adapter Profile whose <c>Command</c> is the built <c>mock-acp</c> executable
    /// completes one Turn: posting a Human message into the Persona's direct Room produces a reply
    /// from the Persona in that same Room, launched and driven entirely through the real
    /// <see cref="AgentProcessLauncher"/> rather than any in-proc substitute.
    /// </summary>
    [Fact]
    public async Task PostAsync_PersonaOnProcessModeAdapter_CompletesOneTurn()
    {
        CancellationToken testCt = TestContext.Current.CancellationToken;
        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(testCt);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(15));
        CancellationToken ct = timeoutSource.Token;

        TempDataDir dataDir = new();
        string pipeName = "process-mode-test-" + Guid.NewGuid().ToString("N");

        Dictionary<string, string?> config = new(StringComparer.Ordinal)
        {
            ["Team:PipeName"] = pipeName,
            ["Team:DataDir"] = dataDir.Path,
            ["Team:HumanName"] = "You",
            ["Team:DemoAgent:Enabled"] = "false",
            ["Team:Acp:Enabled"] = "true",
            ["Team:Acp:Adapters:0:Id"] = "mock-process",

            // Command is the real, built mock-acp executable, so this launches a real child process
            // rather than the in-proc substitute every other conformance test uses. A non-empty Args
            // is what makes AgentProcessOptionsFactory resolve Command directly (its first branch)
            // instead of falling through to AdapterPath or AdapterLocator; the value itself is inert —
            // mock-acp's Program.cs only recognises an optional "--script <path>" pair and ignores
            // anything else.
            ["Team:Acp:Adapters:0:Command"] = MockAdapterExecutablePath,
            ["Team:Acp:Adapters:0:Args:0"] = "--process-mode-test",

            // mock-acp is not the Node adapter, so AgentProcessOptionsFactory must not consult
            // AdapterLocator for it (it would look for the wrong thing entirely).
            ["Team:Acp:Adapters:0:UsesToolNamePrefix"] = "false",
        };

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(config);
        builder.Services.AddTeamServices(builder.Configuration);
        PipeHostFixture.RemovePersonaSupervisorHostedService(builder.Services);

        IHost host = builder.Build();
        await host.StartAsync(ct);

        Persona persona = new("nova", "You are Nova.", Adapter: "mock-process");

        IOptions<TeamOptions> options = host.Services.GetRequiredService<IOptions<TeamOptions>>();
        IAgentHostFactory factory = host.Services.GetRequiredService<IAgentHostFactory>();
        IPromptSource prompts = host.Services.GetRequiredService<IPromptSource>();
        RoomFollows roomFollows = host.Services.GetRequiredService<RoomFollows>();
        ILogger<PersonaRunner> logger = host.Services.GetRequiredService<ILogger<PersonaRunner>>();

        PersonaRunner runner = new(persona, options, factory, prompts, roomFollows, logger);

        try
        {
            await runner.StartAsync(ct);

            ITeamDirectory directory = host.Services.GetRequiredService<ITeamDirectory>();
            User? user = await directory.FindUserByNameAsync("nova", ct);
            Assert.NotNull(user);

            Room? room = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, user.Id, ct);
            Assert.NotNull(room);

            ChatService chat = host.Services.GetRequiredService<ChatService>();
            RoomEvents roomEvents = host.Services.GetRequiredService<RoomEvents>();

            TaskCompletionSource<ChatMessage> repliedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnMessagePosted(MessagePostedEvent posted)
            {
                if (string.Equals(posted.Message.SenderId, user.Id, StringComparison.Ordinal))
                {
                    repliedSource.TrySetResult(posted.Message);
                }
            }

            roomEvents.MessagePosted += OnMessagePosted;
            try
            {
                _ = await chat.PostAsync(room.Id, KnownIds.Human, "hello nova, over a real process", ct: ct);

                ChatMessage reply = await repliedSource.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);

                Assert.Equal(user.Id, reply.SenderId);
            }
            finally
            {
                roomEvents.MessagePosted -= OnMessagePosted;
            }
        }
        finally
        {
            // Disposing the runner disposes its session and its IAgentHost in turn
            // (PersonaRunner.DisposeAsync), which cascades into DotAcpAgentHost.DisposeAsync: that
            // method already waits at most three seconds for the child to exit on its own and calls
            // IAgentProcess.Kill() if it has not, so the real mock-acp process is killed
            // deterministically here rather than left to the test host's own teardown.
            await runner.DisposeAsync();
            await host.StopAsync(CancellationToken.None);
            host.Dispose();
            dataDir.Dispose();
        }
    }

    /// <summary>
    /// The <c>mock-acp</c> executable this same solution build produced, found the same way
    /// <c>MockAdapterTests.MockAdapterExecutablePath</c> does: walk up from
    /// <see cref="AppContext.BaseDirectory"/> to the directory containing <c>Huddle.slnx</c>, then
    /// descend into <c>src/Huddle.MockAdapter</c>'s own build output. The configuration segment
    /// (<c>Debug</c> or <c>Release</c>) and the target-framework segment are read off this test
    /// assembly's own output path rather than hard-coded, so the test follows whichever configuration
    /// it was itself built under, including a <c>Release</c> run.
    /// </summary>
    private static string MockAdapterExecutablePath
    {
        get
        {
            string repoRoot = FindRepoRoot();
            string normalizedBaseDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string targetFramework = Path.GetFileName(normalizedBaseDirectory);
            string? configurationDirectory = Path.GetDirectoryName(normalizedBaseDirectory);
            if (configurationDirectory is null)
            {
                throw new InvalidOperationException($"Could not determine the build configuration from '{AppContext.BaseDirectory}'.");
            }

            string configuration = Path.GetFileName(configurationDirectory);
            string executableName = OperatingSystem.IsWindows() ? "mock-acp.exe" : "mock-acp";
            string executablePath = Path.Combine(repoRoot, "src", "Huddle.MockAdapter", "bin", configuration, targetFramework, executableName);
            if (!File.Exists(executablePath))
            {
                throw new InvalidOperationException($"Could not find the mock-acp executable at '{executablePath}'. Build the solution first.");
            }

            return executablePath;
        }
    }

    /// <summary>Walks up from <see cref="AppContext.BaseDirectory"/> until it finds the directory containing <c>Huddle.slnx</c>.</summary>
    private static string FindRepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "Huddle.slnx");
            if (File.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the repository root (a directory containing 'Huddle.slnx') by walking up from '{AppContext.BaseDirectory}'.");
    }
}
