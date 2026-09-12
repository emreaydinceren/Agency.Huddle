namespace Agency.Huddle.Console;

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Acp.Tools;
using Agency.Huddle.Console.Configuration;
using Agency.Huddle.Console.Terminal;
using Agency.Huddle.Console.Tools;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        // --tools is handled outside ParseArguments: ParseArguments's return shape is pinned by
        // ProgramArgumentTests, so it is stripped here rather than folded into that tuple.
        bool enableTools = args.Contains("--tools", StringComparer.Ordinal);
        string[] remainingArgs = Array.FindAll(args, arg => arg != "--tools");

        // --tools-port pins the loopback port so the URL can be written into a .mcp.json in the
        // session cwd, registering the server through the harness's own settings instead of
        // session/new. Zero means "let the OS choose", which is the normal path.
        int toolsPort = 0;
        int portFlagIndex = Array.IndexOf(remainingArgs, "--tools-port");
        if (portFlagIndex >= 0)
        {
            if (portFlagIndex + 1 >= remainingArgs.Length
                || !int.TryParse(remainingArgs[portFlagIndex + 1], out toolsPort))
            {
                Program.WriteUsage();
                return 1;
            }

            remainingArgs = [.. remainingArgs[..portFlagIndex], .. remainingArgs[(portFlagIndex + 2)..]];
        }

        (string Cwd, bool AutoApprove, bool TraceWire, SystemPromptOptions? SystemPrompt)? parsed = Program.ParseArguments(remainingArgs);
        if (parsed is null)
        {
            return 1;
        }

        System.Console.OutputEncoding = Encoding.UTF8;

        try
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                Args = remainingArgs,
                ContentRootPath = AppContext.BaseDirectory,
            });
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole(consoleOptions => consoleOptions.LogToStandardErrorThreshold = LogLevel.Trace);

            string repoRoot = RepoRootLocator.Locate(AppContext.BaseDirectory)
                ?? throw new InvalidOperationException("Could not locate the repository root (no Huddle.slnx found above the executable).");

            AgentProcessOptions processOptions = AgentProcessOptionsResolver.Resolve(
                builder.Configuration,
                Environment.GetEnvironmentVariable,
                repoRoot);

            string? clientVersion = typeof(Program).Assembly.GetName().Version?.ToString();
            bool traceWire = parsed.Value.TraceWire || builder.Configuration.GetValue<bool>("Acp:TraceWire");

            builder.Services.AddSingleton(processOptions);
            builder.Services.AddSingleton(new DotAcpHostOptions(ClientVersion: clientVersion, TraceWire: traceWire));
            builder.Services.AddSingleton<IAgentProcessLauncher, AgentProcessLauncher>();
            builder.Services.AddSingleton<IAgentHost, DotAcpAgentHost>();
            builder.Services.AddSingleton<IConsoleInput>(new ConsoleLineReader(System.Console.In));
            builder.Services.AddSingleton<IConsoleOutput, SystemConsoleOutput>();
            builder.Services.AddSingleton<ConsoleRenderer>();

            if (parsed.Value.AutoApprove)
            {
                builder.Services.AddSingleton<IPermissionHandler, AutoApprovePermissionHandler>();
            }
            else
            {
                builder.Services.AddSingleton<IPermissionHandler>(sp => new ConsolePermissionHandler(
                    sp.GetRequiredService<IConsoleInput>(),
                    sp.GetRequiredService<IConsoleOutput>(),
                    discardPendingInput: !System.Console.IsInputRedirected));
            }

            if (enableTools)
            {
                // A fresh random token per run so the authenticated path - not the anonymous
                // fallback - is the one actually exercised when running by hand. Never printed
                // or logged.
                string toolsAuthToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

                builder.Services.AddSingleton<ChatRoomRegistry>();
                builder.Services.AddSingleton<IAppTool, ListChatRoomsTool>();
                builder.Services.AddSingleton<IAppTool, CreateChatRoomTool>();
                builder.Services.AddSingleton(sp => new AppToolServer(
                    "team",
                    sp.GetServices<IAppTool>().ToArray(),
                    sp.GetRequiredService<ILoggerFactory>(),
                    toolsPort,
                    toolsAuthToken));
            }

            builder.Services.AddSingleton(new ReplOptions(parsed.Value.Cwd, parsed.Value.AutoApprove, parsed.Value.SystemPrompt, enableTools));
            builder.Services.AddSingleton<Repl>();

            using IHost host = builder.Build();
            Repl repl = host.Services.GetRequiredService<Repl>();

            System.Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                repl.Interrupt();
            };

            return await repl.RunAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await System.Console.Error.WriteLineAsync($"Fatal: {ex.Message}").ConfigureAwait(false);
            return 1;
        }
    }

    internal static (string Cwd, bool AutoApprove, bool TraceWire, SystemPromptOptions? SystemPrompt)? ParseArguments(string[] args)
    {
        string cwd = Environment.CurrentDirectory;
        bool autoApprove = false;
        bool traceWire = false;
        string? systemPromptText = null;
        string? systemPromptFile = null;
        bool systemPromptReplace = false;

        for (int i = 0; i < args.Length; i++)
        {
            // Each value-taking flag below (--cwd, --system-prompt, --system-prompt-file) advances
            // `i` with `args[++i]` to consume the token that holds its value — the standard argv
            // idiom for "this flag takes an argument". Every such advance is bounds-checked by the
            // `i + 1 >= args.Length` guard immediately above it, so `++i` never runs off the end of
            // `args`. This is a deliberate scan-advance onto the flag's value, not an accidental
            // mutation of the loop's stop condition, so S127 is a false positive here for all three
            // cases. One disable/restore around the whole switch (rather than three separate ones)
            // keeps the noise down since all three sites share the same parser and the same reasoning.
#pragma warning disable S127 // Deliberate "consume the flag's value" advances in an argv parser (see comment above), bounds-checked immediately above each one — not the accidental stop-condition mutation the rule guards against.
            switch (args[i])
            {
                case "--cwd":
                    if (i + 1 >= args.Length)
                    {
                        Program.WriteUsage();
                        return null;
                    }

                    cwd = args[++i];
                    break;

                case "--auto-approve":
                    autoApprove = true;
                    break;

                case "--trace-wire":
                    traceWire = true;
                    break;

                case "--system-prompt":
                    if (i + 1 >= args.Length)
                    {
                        Program.WriteUsage();
                        return null;
                    }

                    systemPromptText = args[++i];
                    break;

                case "--system-prompt-file":
                    if (i + 1 >= args.Length)
                    {
                        Program.WriteUsage();
                        return null;
                    }

                    systemPromptFile = args[++i];
                    break;

                case "--system-prompt-replace":
                    systemPromptReplace = true;
                    break;

                default:
                    Program.WriteUsage();
                    return null;
            }
#pragma warning restore S127
        }

        if (systemPromptText is not null && systemPromptFile is not null)
        {
            Program.WriteUsage();
            return null;
        }

        // Whitespace-only inline text is rejected here rather than left to the
        // SystemPromptOptions constructor: ParseArguments runs before Main's
        // try/catch, so an escaping ArgumentException would surface as an
        // unhandled crash instead of usage. Mirrors the file-contents check below.
        if (systemPromptText is not null && string.IsNullOrWhiteSpace(systemPromptText))
        {
            Program.WriteUsage();
            return null;
        }

        string? resolvedText = systemPromptText;
        if (systemPromptFile is not null)
        {
            if (!File.Exists(systemPromptFile))
            {
                Program.WriteUsage();
                return null;
            }

            string fileContents = File.ReadAllText(systemPromptFile);
            if (string.IsNullOrWhiteSpace(fileContents))
            {
                Program.WriteUsage();
                return null;
            }

            resolvedText = fileContents;
        }

        if (resolvedText is null)
        {
            if (systemPromptReplace)
            {
                Program.WriteUsage();
                return null;
            }

            return (Path.GetFullPath(cwd), autoApprove, traceWire, null);
        }

        SystemPromptMode mode = systemPromptReplace ? SystemPromptMode.Replace : SystemPromptMode.Append;
        SystemPromptOptions systemPrompt = new SystemPromptOptions(resolvedText, mode);

        return (Path.GetFullPath(cwd), autoApprove, traceWire, systemPrompt);
    }

    private static void WriteUsage()
    {
        System.Console.Error.WriteLine(
            "Usage: Team.Console [--cwd <path>] [--auto-approve] [--trace-wire] [--tools] "
            + "[--system-prompt <text> | --system-prompt-file <path>] [--system-prompt-replace]");
    }
}
