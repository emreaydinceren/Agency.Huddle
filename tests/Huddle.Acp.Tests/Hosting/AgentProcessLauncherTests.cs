namespace Agency.Huddle.Acp.Tests.Hosting;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Acp.Tests.Fakes;
using Xunit;

[Trait("Category", "Process")]
public sealed class AgentProcessLauncherTests
{
    [Fact(Timeout = 10000)]
    public async Task Launch_EchoesStdinToStdout()
    {
        ListLogger<AgentProcessLauncher> logger = new ListLogger<AgentProcessLauncher>();
        AgentProcessLauncher launcher = new AgentProcessLauncher(logger);
        AgentProcessOptions options = new AgentProcessOptions(
            "node",
            new[] { "-e", "process.stdin.pipe(process.stdout);" });

        IAgentProcess process = launcher.Launch(options);
        try
        {
            byte[] payload = new UTF8Encoding(false).GetBytes("ping\n");
            await process.StandardInput.WriteAsync(payload, TestContext.Current.CancellationToken);
            await process.StandardInput.FlushAsync(TestContext.Current.CancellationToken);

            string? line = await ReadLineAsync(process.StandardOutput, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.Equal("ping", line);
        }
        finally
        {
            process.Kill();
            process.Dispose();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task Launch_DrainsStderrToLogger()
    {
        ListLogger<AgentProcessLauncher> logger = new ListLogger<AgentProcessLauncher>();
        AgentProcessLauncher launcher = new AgentProcessLauncher(logger);
        AgentProcessOptions options = new AgentProcessOptions(
            "node",
            new[] { "-e", "console.error(\"boom\"); process.exit(0);" });

        IAgentProcess process = launcher.Launch(options);
        try
        {
            int exitCode = await process.Exited.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.Equal(0, exitCode);

            bool found = false;
            DateTime deadline = DateTime.UtcNow.AddSeconds(2);
            while (DateTime.UtcNow < deadline)
            {
                if (logger.Entries.Any(entry => entry.Message.Contains("boom", StringComparison.Ordinal)))
                {
                    found = true;
                    break;
                }

                await Task.Delay(50, TestContext.Current.CancellationToken);
            }

            Assert.True(found, "Expected a logged entry containing 'boom'.");
        }
        finally
        {
            process.Kill();
            process.Dispose();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task Launch_AppliesEnvironmentOverrides()
    {
        ListLogger<AgentProcessLauncher> logger = new ListLogger<AgentProcessLauncher>();
        AgentProcessLauncher launcher = new AgentProcessLauncher(logger);
        Dictionary<string, string> overrides = new Dictionary<string, string>
        {
            ["TEAM_TEST"] = "42",
        };
        AgentProcessOptions options = new AgentProcessOptions(
            "node",
            new[] { "-e", "console.log(process.env.TEAM_TEST);" },
            WorkingDirectory: null,
            EnvironmentOverrides: overrides);

        IAgentProcess process = launcher.Launch(options);
        try
        {
            string? line = await ReadLineAsync(process.StandardOutput, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.Equal("42", line);
        }
        finally
        {
            process.Kill();
            process.Dispose();
        }
    }

    [Fact(Timeout = 10000)]
    public void Launch_MissingCommand_ThrowsAgentProcessStartException()
    {
        _ = TestContext.Current.CancellationToken;
        ListLogger<AgentProcessLauncher> logger = new ListLogger<AgentProcessLauncher>();
        AgentProcessLauncher launcher = new AgentProcessLauncher(logger);
        AgentProcessOptions options = new AgentProcessOptions("definitely-not-a-command-xyz", Array.Empty<string>());

        Assert.Throws<AgentProcessStartException>(() =>
        {
            launcher.Launch(options);
        });
    }

    [Fact(Timeout = 10000)]
    public async Task Launch_ExitedCompletesWithExitCode()
    {
        ListLogger<AgentProcessLauncher> logger = new ListLogger<AgentProcessLauncher>();
        AgentProcessLauncher launcher = new AgentProcessLauncher(logger);
        AgentProcessOptions options = new AgentProcessOptions(
            "node",
            new[] { "-e", "process.exit(3);" });

        IAgentProcess process = launcher.Launch(options);
        try
        {
            int exitCode = await process.Exited.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

            Assert.Equal(3, exitCode);
        }
        finally
        {
            process.Kill();
            process.Dispose();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task Launch_WritesUtf8WithoutBom()
    {
        ListLogger<AgentProcessLauncher> logger = new ListLogger<AgentProcessLauncher>();
        AgentProcessLauncher launcher = new AgentProcessLauncher(logger);
        string script = "process.stdin.on(\"data\", d => { process.stdout.write(Buffer.from(d).toString(\"hex\")); });";
        AgentProcessOptions options = new AgentProcessOptions("node", new[] { "-e", script });

        IAgentProcess process = launcher.Launch(options);
        try
        {
            byte[] payload = new UTF8Encoding(false).GetBytes("héllo");
            await process.StandardInput.WriteAsync(payload, TestContext.Current.CancellationToken);
            await process.StandardInput.FlushAsync(TestContext.Current.CancellationToken);

            string hex = await ReadAtLeastAsync(process.StandardOutput, 12, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.StartsWith("68c3a96c6c6f", hex, StringComparison.Ordinal);
            Assert.False(hex.StartsWith("efbbbf", StringComparison.Ordinal));
        }
        finally
        {
            process.Kill();
            process.Dispose();
        }
    }

    [Fact(Timeout = 10000)]
    public async Task Kill_TerminatesRunningProcess()
    {
        ListLogger<AgentProcessLauncher> logger = new ListLogger<AgentProcessLauncher>();
        AgentProcessLauncher launcher = new AgentProcessLauncher(logger);
        AgentProcessOptions options = new AgentProcessOptions(
            "node",
            new[] { "-e", "setInterval(() => {}, 1000);" });

        IAgentProcess process = launcher.Launch(options);
        try
        {
            Task<int> exitedTask = process.Exited;

            process.Kill();

            await exitedTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.True(exitedTask.IsCompletedSuccessfully);
        }
        finally
        {
            process.Dispose();
        }
    }

    private static async Task<string?> ReadLineAsync(Stream stream, TimeSpan timeout, CancellationToken cancellationToken)
    {
        string text = await ReadUntilAsync(stream, timeout, builder => builder.ToString().Contains('\n'), cancellationToken);
        if (text.Length == 0)
        {
            return null;
        }

        return text.TrimEnd('\r', '\n');
    }

    private static async Task<string> ReadAtLeastAsync(Stream stream, int minimumLength, TimeSpan timeout, CancellationToken cancellationToken)
    {
        return await ReadUntilAsync(stream, timeout, builder => builder.Length >= minimumLength, cancellationToken);
    }

    private static async Task<string> ReadUntilAsync(Stream stream, TimeSpan timeout, Func<StringBuilder, bool> isDone, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        StringBuilder builder = new StringBuilder();
        byte[] buffer = new byte[256];
        char[] chars = new char[256];
        Decoder decoder = new UTF8Encoding(false).GetDecoder();

        try
        {
            while (!isDone(builder))
            {
                int read = await stream.ReadAsync(buffer, timeoutCts.Token);
                if (read <= 0)
                {
                    break;
                }

                int charCount = decoder.GetChars(buffer, 0, read, chars, 0);
                builder.Append(chars, 0, charCount);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }

        return builder.ToString();
    }
}
