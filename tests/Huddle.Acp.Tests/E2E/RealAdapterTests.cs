namespace Agency.Huddle.Acp.Tests.E2E;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Acp.Tests.Fakes;
using Xunit;

[Collection("E2E")]
public sealed class RealAdapterTests
{
    [Fact(Timeout = 180000, SkipUnless = nameof(E2E.Enabled), SkipType = typeof(E2E), Skip = "Set TEAM_E2E=1 to run")]
    public async Task RealAdapter_Initialize_ReportsAgentInfo()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string workingDirectory = RealAdapterTests.CreateTempWorkingDirectory();
        ListLoggerFactory loggerFactory = new ListLoggerFactory();
        CapturingProcessLauncher launcher = new CapturingProcessLauncher(
            new AgentProcessLauncher(loggerFactory.CreateLogger<AgentProcessLauncher>()));
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("node", [E2E.AdapterEntry], E2E.RepoRoot),
            launcher,
            new DotAcpHostOptions(TraceWire: true),
            loggerFactory);

        try
        {
            await host.StartAsync(cancellationToken);

            Assert.True(
                !string.IsNullOrEmpty(host.Info.AgentName),
                $"Expected a non-empty agent name.{Environment.NewLine}{RealAdapterTests.DescribeWireLog(loggerFactory)}");
            Assert.Equal(1, host.Info.ProtocolVersion);
        }
        finally
        {
            await host.DisposeAsync();
            await RealAdapterTests.DeleteTempWorkingDirectoryAsync(workingDirectory, cancellationToken);
        }
    }

    [Fact(Timeout = 180000, SkipUnless = nameof(E2E.Enabled), SkipType = typeof(E2E), Skip = "Set TEAM_E2E=1 to run")]
    public async Task RealAdapter_Prompt_StreamsPongAndEndsTurn()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string workingDirectory = RealAdapterTests.CreateTempWorkingDirectory();
        ListLoggerFactory loggerFactory = new ListLoggerFactory();
        CapturingProcessLauncher launcher = new CapturingProcessLauncher(
            new AgentProcessLauncher(loggerFactory.CreateLogger<AgentProcessLauncher>()));
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("node", [E2E.AdapterEntry], E2E.RepoRoot),
            launcher,
            new DotAcpHostOptions(TraceWire: true),
            loggerFactory);
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);
            session = await host.StartSessionAsync(
                new AgentSessionOptions(workingDirectory, new AutoApprovePermissionHandler()),
                cancellationToken);

            PromptResult result = await session.PromptAsync(
                "Reply with exactly the word PONG and nothing else.",
                cancellationToken);

            string combinedText = RealAdapterTests.CombineMessageText(RealAdapterTests.DrainEvents(session));

            Assert.True(
                combinedText.Contains("PONG", StringComparison.OrdinalIgnoreCase),
                $"Expected the concatenated reply to contain 'PONG'. Actual reply: '{combinedText}'.{Environment.NewLine}{RealAdapterTests.DescribeWireLog(loggerFactory)}");
            Assert.Equal(StopReason.EndTurn, result.StopReason);
        }
        finally
        {
            if (session is not null)
            {
                await session.DisposeAsync();
            }

            await host.DisposeAsync();
            await RealAdapterTests.DeleteTempWorkingDirectoryAsync(workingDirectory, cancellationToken);
        }
    }

    [Fact(Timeout = 180000, SkipUnless = nameof(E2E.Enabled), SkipType = typeof(E2E), Skip = "Set TEAM_E2E=1 to run")]
    public async Task RealAdapter_ReplaceSystemPrompt_ForcesExactBananaReply()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string workingDirectory = RealAdapterTests.CreateTempWorkingDirectory();
        ListLoggerFactory loggerFactory = new ListLoggerFactory();
        CapturingProcessLauncher launcher = new CapturingProcessLauncher(
            new AgentProcessLauncher(loggerFactory.CreateLogger<AgentProcessLauncher>()));
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("node", [E2E.AdapterEntry], E2E.RepoRoot),
            launcher,
            new DotAcpHostOptions(TraceWire: true),
            loggerFactory);
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);
            SystemPromptOptions systemPrompt = new SystemPromptOptions(
                "You must reply with exactly the single word BANANA and nothing else.",
                SystemPromptMode.Replace);
            session = await host.StartSessionAsync(
                new AgentSessionOptions(workingDirectory, new AutoApprovePermissionHandler(), systemPrompt),
                cancellationToken);

            await session.PromptAsync("hello", cancellationToken);

            string combinedText = RealAdapterTests.CombineMessageText(RealAdapterTests.DrainEvents(session));

            Assert.True(
                combinedText.Contains("BANANA", StringComparison.OrdinalIgnoreCase),
                $"Expected the concatenated reply to contain 'BANANA'. Actual reply: '{combinedText}'.{Environment.NewLine}{RealAdapterTests.DescribeWireLog(loggerFactory)}");
        }
        finally
        {
            if (session is not null)
            {
                await session.DisposeAsync();
            }

            await host.DisposeAsync();
            await RealAdapterTests.DeleteTempWorkingDirectoryAsync(workingDirectory, cancellationToken);
        }
    }

    [Fact(Timeout = 180000, SkipUnless = nameof(E2E.Enabled), SkipType = typeof(E2E), Skip = "Set TEAM_E2E=1 to run")]
    public async Task RealAdapter_Cancel_ReturnsCancelledStopReason()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string workingDirectory = RealAdapterTests.CreateTempWorkingDirectory();
        ListLoggerFactory loggerFactory = new ListLoggerFactory();
        CapturingProcessLauncher launcher = new CapturingProcessLauncher(
            new AgentProcessLauncher(loggerFactory.CreateLogger<AgentProcessLauncher>()));
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("node", [E2E.AdapterEntry], E2E.RepoRoot),
            launcher,
            new DotAcpHostOptions(TraceWire: true),
            loggerFactory);
        IAgentSession? session = null;

        try
        {
            await host.StartAsync(cancellationToken);
            session = await host.StartSessionAsync(
                new AgentSessionOptions(workingDirectory, new AutoApprovePermissionHandler()),
                cancellationToken);

            Task<PromptResult> promptTask = session.PromptAsync(
                "Count slowly from 1 to 500, one number per line, do not summarise.",
                cancellationToken);

            await RealAdapterTests.WaitForFirstMessageChunkAsync(
                session,
                TimeSpan.FromSeconds(60),
                loggerFactory,
                cancellationToken);

            await session.CancelAsync(cancellationToken);

            PromptResult result = await promptTask.WaitAsync(TimeSpan.FromSeconds(60), cancellationToken);

            Assert.True(
                result.StopReason == StopReason.Cancelled,
                $"Expected stop reason Cancelled but observed {result.StopReason}.{Environment.NewLine}{RealAdapterTests.DescribeWireLog(loggerFactory)}");
        }
        finally
        {
            if (session is not null)
            {
                await session.DisposeAsync();
            }

            await host.DisposeAsync();
            await RealAdapterTests.DeleteTempWorkingDirectoryAsync(workingDirectory, cancellationToken);
        }
    }

    [Fact(Timeout = 180000, SkipUnless = nameof(E2E.Enabled), SkipType = typeof(E2E), Skip = "Set TEAM_E2E=1 to run")]
    public async Task RealAdapter_Dispose_LeavesNoNodeProcess()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string workingDirectory = RealAdapterTests.CreateTempWorkingDirectory();
        ListLoggerFactory loggerFactory = new ListLoggerFactory();
        CapturingProcessLauncher launcher = new CapturingProcessLauncher(
            new AgentProcessLauncher(loggerFactory.CreateLogger<AgentProcessLauncher>()));
        DotAcpAgentHost host = new DotAcpAgentHost(
            new AgentProcessOptions("node", [E2E.AdapterEntry], E2E.RepoRoot),
            launcher,
            new DotAcpHostOptions(TraceWire: true),
            loggerFactory);

        try
        {
            await host.StartAsync(cancellationToken);

            Assert.NotNull(launcher.LastProcess);
            int processId = RealAdapterTests.GetProcessId(launcher.LastProcess);

            await host.DisposeAsync();

            Assert.True(
                !RealAdapterTests.IsProcessRunning(processId),
                $"Expected process {processId} (the one this test launched) to no longer be running after DisposeAsync.{Environment.NewLine}{RealAdapterTests.DescribeWireLog(loggerFactory)}");
        }
        finally
        {
            await host.DisposeAsync();
            await RealAdapterTests.DeleteTempWorkingDirectoryAsync(workingDirectory, cancellationToken);
        }
    }

    [Fact(Timeout = 180000, SkipUnless = nameof(E2E.Enabled), SkipType = typeof(E2E), Skip = "Set TEAM_E2E=1 to run")]
    public async Task RealAdapter_TwoConcurrentHosts_KeepSeparatePersonasAndSessions()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string workingDirectoryAlpha = RealAdapterTests.CreateTempWorkingDirectory();
        string workingDirectoryBravo = RealAdapterTests.CreateTempWorkingDirectory();
        ListLoggerFactory loggerFactoryAlpha = new ListLoggerFactory();
        ListLoggerFactory loggerFactoryBravo = new ListLoggerFactory();
        CapturingProcessLauncher launcherAlpha = new CapturingProcessLauncher(
            new AgentProcessLauncher(loggerFactoryAlpha.CreateLogger<AgentProcessLauncher>()));
        CapturingProcessLauncher launcherBravo = new CapturingProcessLauncher(
            new AgentProcessLauncher(loggerFactoryBravo.CreateLogger<AgentProcessLauncher>()));
        DotAcpAgentHost hostAlpha = new DotAcpAgentHost(
            new AgentProcessOptions("node", [E2E.AdapterEntry], E2E.RepoRoot),
            launcherAlpha,
            new DotAcpHostOptions(TraceWire: true),
            loggerFactoryAlpha);
        DotAcpAgentHost hostBravo = new DotAcpAgentHost(
            new AgentProcessOptions("node", [E2E.AdapterEntry], E2E.RepoRoot),
            launcherBravo,
            new DotAcpHostOptions(TraceWire: true),
            loggerFactoryBravo);
        IAgentSession? sessionAlpha = null;
        IAgentSession? sessionBravo = null;

        try
        {
            await Task.WhenAll(
                hostAlpha.StartAsync(cancellationToken),
                hostBravo.StartAsync(cancellationToken));

            SystemPromptOptions personaAlpha = new SystemPromptOptions(
                "You are ALPHA. No matter what you are asked, reply with exactly the single word ALPHA and nothing else.",
                SystemPromptMode.Replace);
            SystemPromptOptions personaBravo = new SystemPromptOptions(
                "You are BRAVO. No matter what you are asked, reply with exactly the single word BRAVO and nothing else.",
                SystemPromptMode.Replace);

            Task<IAgentSession> startAlpha = hostAlpha.StartSessionAsync(
                new AgentSessionOptions(workingDirectoryAlpha, new AutoApprovePermissionHandler(), personaAlpha),
                cancellationToken);
            Task<IAgentSession> startBravo = hostBravo.StartSessionAsync(
                new AgentSessionOptions(workingDirectoryBravo, new AutoApprovePermissionHandler(), personaBravo),
                cancellationToken);
            await Task.WhenAll(startAlpha, startBravo);
            sessionAlpha = startAlpha.Result;
            sessionBravo = startBravo.Result;

            Task<PromptResult> promptAlpha = sessionAlpha.PromptAsync("Who are you?", cancellationToken);
            Task<PromptResult> promptBravo = sessionBravo.PromptAsync("Who are you?", cancellationToken);
            await Task.WhenAll(promptAlpha, promptBravo);

            string combinedTextAlpha = RealAdapterTests.CombineMessageText(RealAdapterTests.DrainEvents(sessionAlpha));
            string combinedTextBravo = RealAdapterTests.CombineMessageText(RealAdapterTests.DrainEvents(sessionBravo));

            Assert.True(
                combinedTextAlpha.Contains("ALPHA", StringComparison.OrdinalIgnoreCase)
                    && !combinedTextAlpha.Contains("BRAVO", StringComparison.OrdinalIgnoreCase),
                $"Expected host A's reply to contain ALPHA and not BRAVO. Actual reply: '{combinedTextAlpha}'.{Environment.NewLine}{RealAdapterTests.DescribeWireLog(loggerFactoryAlpha)}");
            Assert.True(
                combinedTextBravo.Contains("BRAVO", StringComparison.OrdinalIgnoreCase)
                    && !combinedTextBravo.Contains("ALPHA", StringComparison.OrdinalIgnoreCase),
                $"Expected host B's reply to contain BRAVO and not ALPHA. Actual reply: '{combinedTextBravo}'.{Environment.NewLine}{RealAdapterTests.DescribeWireLog(loggerFactoryBravo)}");
            Assert.True(
                !string.Equals(sessionAlpha.SessionId, sessionBravo.SessionId, StringComparison.Ordinal),
                $"Expected the two real adapters to hand back different session ids, but both were '{sessionAlpha.SessionId}'.");

            await sessionAlpha.DisposeAsync();
            sessionAlpha = null;
            await hostAlpha.DisposeAsync();

            PromptResult secondBravoResult = await sessionBravo.PromptAsync("Who are you?", cancellationToken);
            string secondCombinedTextBravo = RealAdapterTests.CombineMessageText(RealAdapterTests.DrainEvents(sessionBravo));

            Assert.True(
                secondCombinedTextBravo.Contains("BRAVO", StringComparison.OrdinalIgnoreCase),
                $"Expected host B to still answer BRAVO after host A was disposed. Actual reply: '{secondCombinedTextBravo}'.{Environment.NewLine}{RealAdapterTests.DescribeWireLog(loggerFactoryBravo)}");
            Assert.Equal(StopReason.EndTurn, secondBravoResult.StopReason);
        }
        finally
        {
            if (sessionAlpha is not null)
            {
                await sessionAlpha.DisposeAsync();
            }

            if (sessionBravo is not null)
            {
                await sessionBravo.DisposeAsync();
            }

            await hostAlpha.DisposeAsync();
            await hostBravo.DisposeAsync();
            await RealAdapterTests.DeleteTempWorkingDirectoryAsync(workingDirectoryAlpha, cancellationToken);
            await RealAdapterTests.DeleteTempWorkingDirectoryAsync(workingDirectoryBravo, cancellationToken);
        }
    }

    private static async Task WaitForFirstMessageChunkAsync(
        IAgentSession session,
        TimeSpan timeout,
        ListLoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            while (true)
            {
                AgentEvent agentEvent = await session.Events.ReadAsync(timeoutCts.Token);
                if (agentEvent is MessageChunk)
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Assert.Fail(
                $"Timed out after {timeout} waiting for the first MessageChunk.{Environment.NewLine}{RealAdapterTests.DescribeWireLog(loggerFactory)}");
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

    private static int GetProcessId(IAgentProcess agentProcess)
    {
        FieldInfo? field = agentProcess.GetType().GetField("process", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field?.GetValue(agentProcess) is not Process nativeProcess)
        {
            throw new InvalidOperationException(
                $"Could not read the underlying process handle from '{agentProcess.GetType().FullName}' via reflection.");
        }

        return nativeProcess.Id;
    }

    private static bool IsProcessRunning(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string CreateTempWorkingDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "team-acp-e2e-" + Guid.NewGuid().ToString("N"));
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

    private sealed class CapturingProcessLauncher(IAgentProcessLauncher inner) : IAgentProcessLauncher
    {
        internal IAgentProcess? LastProcess { get; private set; }

        public IAgentProcess Launch(AgentProcessOptions options)
        {
            IAgentProcess process = inner.Launch(options);
            this.LastProcess = process;
            return process;
        }
    }
}
