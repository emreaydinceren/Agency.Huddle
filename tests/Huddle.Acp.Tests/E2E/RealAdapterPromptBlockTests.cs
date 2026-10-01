namespace Agency.Huddle.Acp.Tests.E2E;

using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Acp.Tests.Fakes;
using Xunit;

/// <summary>
/// The paid live checks of the Prompt blocks design (Appendix C, V-1, V-2, V-4 and the Phase 5 text
/// resource): a real <c>claude-agent-acp</c> is sent real image and resource blocks through
/// <see cref="DotAcpAgentSession"/>. Each test spends a small amount of money, so every one is gated
/// on <c>TEAM_E2E=1</c> like the rest of this folder.
/// </summary>
[Collection("E2E")]
public sealed class RealAdapterPromptBlockTests
{
    /// <summary>The adapter advertises <c>image</c> and <c>embeddedContext</c> at <c>initialize</c>, and a solid red picture is described as red: the bytes reach the model.</summary>
    [Fact(Timeout = 240000, SkipUnless = nameof(E2E.Enabled), SkipType = typeof(E2E), Skip = "Set TEAM_E2E=1 to run")]
    public async Task RealAdapter_ImageBlock_SolidRedPicture_IsDescribedAsRed()
    {
        await using LiveSession live = await LiveSession.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(live.Host.Info.PromptCapabilities?.Image, "The adapter did not advertise promptCapabilities.image.");
        Assert.True(live.Host.Info.PromptCapabilities?.EmbeddedContext, "The adapter did not advertise promptCapabilities.embeddedContext.");

        AgentPrompt prompt = new(
            "What single colour fills this image? Answer with one lowercase word and nothing else.",
            [new AgentImageBlock("image/png", TestPng.Solid(64, 64, 255, 0, 0))]);

        (PromptResult result, string reply) = await live.PromptAsync(prompt, TestContext.Current.CancellationToken);

        TestContext.Current.SendDiagnosticMessage($"V-1 solid red: stop={result.StopReason} reply='{reply}' usage={live.LastUsage}");
        Assert.Equal(StopReason.EndTurn, result.StopReason);
        Assert.Contains("red", reply, StringComparison.OrdinalIgnoreCase); // contains-ok: model reply text, not markup
    }

    /// <summary>
    /// V-1 and V-2 at the design's per-image limit: one picture a little under 3 MiB (about 4 MB once
    /// base64-encoded) goes through the writer, the adapter's reader and the provider and the Turn ends
    /// normally.
    /// </summary>
    [Fact(Timeout = 300000, SkipUnless = nameof(E2E.Enabled), SkipType = typeof(E2E), Skip = "Set TEAM_E2E=1 to run")]
    public async Task RealAdapter_ImageBlock_AtThePerImageLimit_EndsTheTurn()
    {
        await using LiveSession live = await LiveSession.StartAsync(TestContext.Current.CancellationToken);
        byte[] png = TestPng.Noise(1000, 1000, seed: 1);
        Assert.InRange(png.Length, 2_500_000, 3 * 1024 * 1024);
        AgentPrompt prompt = new("Reply with the single word OK.", [new AgentImageBlock("image/png", png)]);

        (PromptResult result, string reply) = await live.PromptAsync(prompt, TestContext.Current.CancellationToken);

        TestContext.Current.SendDiagnosticMessage($"V-1 limit: bytes={png.Length} stop={result.StopReason} reply='{reply}' usage={live.LastUsage}");
        Assert.Equal(StopReason.EndTurn, result.StopReason);
    }

    /// <summary>V-2: four pictures of about 2 MB each make a <c>session/prompt</c> line of about 10 MB; it crosses the writer and the adapter's reader intact and the Turn ends.</summary>
    [Fact(Timeout = 420000, SkipUnless = nameof(E2E.Enabled), SkipType = typeof(E2E), Skip = "Set TEAM_E2E=1 to run")]
    public async Task RealAdapter_FourImageBlocks_AboutTenMegabyteLine_EndsTheTurn()
    {
        await using LiveSession live = await LiveSession.StartAsync(TestContext.Current.CancellationToken);
        AgentPromptBlock[] blocks = [.. Enumerable.Range(1, 4).Select(i => (AgentPromptBlock)new AgentImageBlock("image/png", TestPng.Noise(810, 810, seed: i)))];
        long total = blocks.Sum(block => ((AgentImageBlock)block).Data.Length);
        AgentPrompt prompt = new("There are four pictures. Reply with the single word FOUR.", blocks);

        (PromptResult result, string reply) = await live.PromptAsync(prompt, TestContext.Current.CancellationToken);

        TestContext.Current.SendDiagnosticMessage($"V-2 four images: rawBytes={total} stop={result.StopReason} reply='{reply}' usage={live.LastUsage}");
        Assert.Equal(StopReason.EndTurn, result.StopReason);
    }

    /// <summary>
    /// V-4: a real header with a body cut off partway. The adapter either answers or the provider
    /// refuses; either way the Turn must end once and cleanly, not hang. Whatever it did is recorded in
    /// the diagnostic message.
    /// </summary>
    [Fact(Timeout = 240000, SkipUnless = nameof(E2E.Enabled), SkipType = typeof(E2E), Skip = "Set TEAM_E2E=1 to run")]
    public async Task RealAdapter_ImageBlock_TruncatedBodyBehindAValidHeader_EndsCleanly()
    {
        await using LiveSession live = await LiveSession.StartAsync(TestContext.Current.CancellationToken);
        byte[] whole = TestPng.Noise(200, 200, seed: 7);
        byte[] truncated = whole[..(whole.Length / 2)];
        AgentPrompt prompt = new("Describe this picture in one short sentence.", [new AgentImageBlock("image/png", truncated)]);

        string outcome;
        try
        {
            (PromptResult result, string reply) = await live.PromptAsync(prompt, TestContext.Current.CancellationToken);
            outcome = $"completed stop={result.StopReason} reply='{reply}'";
        }
        catch (AgentException ex)
        {
            outcome = $"AgentException: {ex.Message}";
        }

        RealAdapterPromptBlockTests.Record($"V-4 truncated body: {outcome}");
        Assert.False(string.IsNullOrEmpty(outcome));
    }

    /// <summary>Phase 5: a text resource reaches the model, which the adapter turns into a link plus a <c>&lt;context&gt;</c> block; the model reads the code word out of it.</summary>
    [Fact(Timeout = 240000, SkipUnless = nameof(E2E.Enabled), SkipType = typeof(E2E), Skip = "Set TEAM_E2E=1 to run")]
    public async Task RealAdapter_TextResourceBlock_CodeWordIsReadByTheModel()
    {
        await using LiveSession live = await LiveSession.StartAsync(TestContext.Current.CancellationToken);
        AgentPrompt prompt = new(
            "What is the code word in the attached document? Reply with the code word only.",
            [new AgentTextResourceBlock("file:///E:/Huddle/notes.md", "text/markdown", "# Notes\n\nThe code word is ZEBRA-4821.\n")]);

        (PromptResult result, string reply) = await live.PromptAsync(prompt, TestContext.Current.CancellationToken);

        TestContext.Current.SendDiagnosticMessage($"Phase 5 resource: stop={result.StopReason} reply='{reply}' usage={live.LastUsage}");
        Assert.Equal(StopReason.EndTurn, result.StopReason);
        Assert.Contains("ZEBRA-4821", reply, StringComparison.Ordinal); // contains-ok: model reply text, not markup
    }

    /// <summary>
    /// Appends what a live check observed to <c>prompt-blocks-live.log</c> in the temp folder: the test
    /// runner does not print diagnostic messages, and these figures are the point of the run.
    /// </summary>
    /// <param name="line">The observation.</param>
    internal static void Record(string line)
    {
        File.AppendAllText(Path.Combine(Path.GetTempPath(), "prompt-blocks-live.log"), $"{DateTimeOffset.Now:O} {line}{Environment.NewLine}");
    }

    private sealed class LiveSession : IAsyncDisposable
    {
        private LiveSession(DotAcpAgentHost host, IAgentSession session, string workingDirectory)
        {
            this.Host = host;
            this.Session = session;
            this.WorkingDirectory = workingDirectory;
        }

        internal DotAcpAgentHost Host { get; }

        internal IAgentSession Session { get; }

        internal string WorkingDirectory { get; }

        internal string LastUsage { get; private set; } = "none";

        internal static async Task<LiveSession> StartAsync(CancellationToken cancellationToken)
        {
            string workingDirectory = Path.Combine(Path.GetTempPath(), "team-acp-e2e-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workingDirectory);
            ListLoggerFactory loggerFactory = new();
            DotAcpAgentHost host = new(
                new AgentProcessOptions("node", [E2E.AdapterEntry], E2E.RepoRoot),
                new AgentProcessLauncher(loggerFactory.CreateLogger<AgentProcessLauncher>()),
                new DotAcpHostOptions(),
                loggerFactory);
            await host.StartAsync(cancellationToken);
            IAgentSession session = await host.StartSessionAsync(
                new AgentSessionOptions(workingDirectory, new AutoApprovePermissionHandler()),
                cancellationToken);
            return new LiveSession(host, session, workingDirectory);
        }

        internal async Task<(PromptResult Result, string Reply)> PromptAsync(AgentPrompt prompt, CancellationToken cancellationToken)
        {
            PromptResult result = await this.Session.PromptAsync(prompt, cancellationToken);
            StringBuilder reply = new();
            while (this.Session.Events.TryRead(out AgentEvent? agentEvent))
            {
                if (agentEvent is MessageChunk chunk)
                {
                    reply.Append(chunk.Text);
                }
                else if (agentEvent is UsageUpdated usage)
                {
                    this.LastUsage = $"used={usage.Used}/{usage.Size}";
                }
            }

            string text = reply.ToString().Trim();
            long rawBytes = (prompt.Blocks ?? []).OfType<AgentImageBlock>().Sum(block => (long)block.Data.Length);
            RealAdapterPromptBlockTests.Record($"prompt='{prompt.Text}' blocks={prompt.Blocks?.Count ?? 0} rawImageBytes={rawBytes} stop={result.StopReason} usage={this.LastUsage} reply='{text}'");
            return (result, text);
        }

        public async ValueTask DisposeAsync()
        {
            await this.Session.DisposeAsync();
            await this.Host.DisposeAsync();
            try
            {
                Directory.Delete(this.WorkingDirectory, recursive: true);
            }
            catch (IOException)
            {
                // The killed adapter can hold its working directory open briefly; a leaked temp folder is harmless here.
            }
            catch (UnauthorizedAccessException)
            {
                // Same: the folder is under the OS temp directory and is not worth failing a passed check for.
            }
        }
    }

    /// <summary>Writes real, decodable PNGs: the provider decodes the picture, so a header-only fixture would not do.</summary>
    private static class TestPng
    {
        internal static byte[] Solid(int width, int height, byte red, byte green, byte blue)
        {
            byte[] rows = new byte[height * ((width * 3) + 1)];
            for (int y = 0; y < height; y++)
            {
                int row = y * ((width * 3) + 1);
                for (int x = 0; x < width; x++)
                {
                    rows[row + 1 + (x * 3)] = red;
                    rows[row + 2 + (x * 3)] = green;
                    rows[row + 3 + (x * 3)] = blue;
                }
            }

            return Encode(width, height, rows, CompressionLevel.Optimal);
        }

        internal static byte[] Noise(int width, int height, int seed)
        {
            byte[] rows = new byte[height * ((width * 3) + 1)];
            Random random = new(seed);
            for (int y = 0; y < height; y++)
            {
                random.NextBytes(rows.AsSpan((y * ((width * 3) + 1)) + 1, width * 3));
            }

            return Encode(width, height, rows, CompressionLevel.NoCompression);
        }

        private static byte[] Encode(int width, int height, byte[] scanlines, CompressionLevel level)
        {
            using MemoryStream png = new();
            png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

            byte[] header = new byte[13];
            WriteBigEndian(header, 0, width);
            WriteBigEndian(header, 4, height);
            header[8] = 8;
            header[9] = 2;
            WriteChunk(png, "IHDR", header);

            using MemoryStream compressed = new();
            using (ZLibStream zlib = new(compressed, level, leaveOpen: true))
            {
                zlib.Write(scanlines);
            }

            WriteChunk(png, "IDAT", compressed.ToArray());
            WriteChunk(png, "IEND", []);
            return png.ToArray();
        }

        private static void WriteChunk(Stream stream, string type, byte[] data)
        {
            byte[] length = new byte[4];
            WriteBigEndian(length, 0, data.Length);
            stream.Write(length);
            byte[] typeBytes = Encoding.ASCII.GetBytes(type);
            stream.Write(typeBytes);
            stream.Write(data);
            uint crc = Crc32(typeBytes, data);
            byte[] crcBytes = new byte[4];
            WriteBigEndian(crcBytes, 0, unchecked((int)crc));
            stream.Write(crcBytes);
        }

        private static void WriteBigEndian(byte[] target, int offset, int value)
        {
            target[offset] = (byte)(value >> 24);
            target[offset + 1] = (byte)(value >> 16);
            target[offset + 2] = (byte)(value >> 8);
            target[offset + 3] = (byte)value;
        }

        private static uint Crc32(byte[] type, byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            foreach (byte value in type)
            {
                crc = Step(crc, value);
            }

            foreach (byte value in data)
            {
                crc = Step(crc, value);
            }

            return ~crc;
        }

        private static uint Step(uint crc, byte value)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            }

            return crc;
        }
    }
}
