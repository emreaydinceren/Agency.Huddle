namespace Agency.Huddle.Acp.Tests.E2E;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Acp.Tests.Fakes;
using Agency.Huddle.Acp.Tools;
using Agency.Huddle.Console.Tools;
using Xunit;

/// <summary>
/// The one test that proves a live model discovers and invokes a tool whose body runs inside
/// our own process, reached only through the real adapter and a real MCP-over-HTTP round trip.
/// Never runs unless TEAM_E2E=1 is set - it spends real model tokens on the user's subscription.
/// </summary>
[Collection("E2E")]
public sealed class RealAdapterAppToolTests
{
    [Fact(Timeout = 180000, SkipUnless = nameof(E2E.Enabled), SkipType = typeof(E2E), Skip = "Set TEAM_E2E=1 to run")]
    public async Task RealAdapter_ChatRoomTool_IsCalledByLiveModel()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string workingDirectory = RealAdapterAppToolTests.CreateTempWorkingDirectory();
        ListLoggerFactory loggerFactory = new ListLoggerFactory();
        ChatRoomRegistry registry = new ChatRoomRegistry();
        AppToolServer toolServer = new AppToolServer(
            "team", [new ListChatRoomsTool(registry), new CreateChatRoomTool(registry)], loggerFactory);
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("node", [E2E.AdapterEntry], E2E.RepoRoot),
            new AgentProcessLauncher(loggerFactory.CreateLogger<AgentProcessLauncher>()),
            new DotAcpHostOptions(TraceWire: true),
            loggerFactory);
        IAgentSession? session = null;

        try
        {
            // The tool server must already be listening before session/new carries its URL.
            await toolServer.StartAsync(cancellationToken);
            await host.StartAsync(cancellationToken);
            session = await host.StartSessionAsync(
                new AgentSessionOptions(
                    workingDirectory,
                    new AutoApprovePermissionHandler(),
                    RealAdapterAppToolTests.ToolDiscoveryPrompt(toolServer),
                    toolServer.Endpoint),
                cancellationToken);

            await session.PromptAsync("What chat rooms exist? List them.", cancellationToken);

            string combinedText = RealAdapterAppToolTests.CombineMessageText(RealAdapterAppToolTests.DrainEvents(session));

            Assert.True(
                combinedText.Contains("bananas", StringComparison.OrdinalIgnoreCase),
                $"Expected the concatenated reply to contain 'bananas'. Actual reply: '{combinedText}'.{Environment.NewLine}{RealAdapterAppToolTests.DescribeWireLog(loggerFactory)}");
        }
        finally
        {
            if (session is not null)
            {
                await session.DisposeAsync();
            }

            await host.DisposeAsync();
            await toolServer.DisposeAsync();
            await RealAdapterAppToolTests.DeleteTempWorkingDirectoryAsync(workingDirectory, cancellationToken);
        }
    }

    private static List<AgentEvent> DrainEvents(IAgentSession session)
    {
        List<AgentEvent> events = new List<AgentEvent>();
        while (session.Events.TryRead(out AgentEvent? agentEvent))
        {
            events.Add(agentEvent);
        }

        return events;
    }

    private static string CombineMessageText(IEnumerable<AgentEvent> events)
    {
        StringBuilder builder = new StringBuilder();
        foreach (AgentEvent agentEvent in events)
        {
            if (agentEvent is MessageChunk chunk)
            {
                builder.Append(chunk.Text);
            }
        }

        return builder.ToString();
    }

    private static string DescribeWireLog(ListLoggerFactory loggerFactory)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("Wire log:");
        foreach (LogEntry entry in loggerFactory.Logger.Entries)
        {
            builder.Append(Environment.NewLine);
            builder.Append('[').Append(entry.Level).Append("] ").Append(entry.Message);
        }

        return builder.ToString();
    }

    private static string CreateTempWorkingDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "team-acp-e2e-apptool-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task DeleteTempWorkingDirectoryAsync(string path, CancellationToken cancellationToken)
    {
        const int maxAttempts = 15;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                // The OS can briefly keep the killed node process's working-directory handle open
                // after Kill() returns; retry until the handle is released rather than leaking the folder.
                await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
            }
            catch (UnauthorizedAccessException) when (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
            }
        }
    }

    /// <summary>
    /// Names the app tools in the system prompt so the test does not depend on the model
    /// discovering them unaided.
    /// <para>
    /// Registering a tool is not the same as the model finding it. On an account with many MCP
    /// servers the session runs in deferred-tool mode, where the model resolves tools through a
    /// name lookup rather than scanning the roster, and an unqualified or guessed name simply
    /// misses. Verified 2026-09-10: the identical prompt failed without this and succeeded with
    /// it, against the same running server. Without it, whether this test passes depends on the
    /// tool roster of whoever runs it.
    /// </para>
    /// </summary>
    private static SystemPromptOptions ToolDiscoveryPrompt(AppToolServer toolServer)
    {
        string names = string.Join(", ", toolServer.ToolNames.Select(name => $"mcp__team__{name}"));
        return new SystemPromptOptions(
            $"The Team application exposes these tools, which run inside the application process: {names}. "
            + "When asked about chat rooms, call them. Never answer from the codebase, and never "
            + "search source files to answer a question about chat rooms.",
            SystemPromptMode.Append);
    }
}
