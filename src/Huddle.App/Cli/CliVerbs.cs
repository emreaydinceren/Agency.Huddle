using System.Text.Json;
using Agency.Huddle.App.Desktop;
using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.App.Cli;

/// <summary>The client verbs of Spec 6.13 (<c>auth-token</c>, <c>dashboard</c>, <c>devices</c>), run before the host is built.</summary>
internal static class CliVerbs
{
    /// <summary>The usage text; also the <c>--help</c>-less answer to an unknown verb.</summary>
    internal const string Usage = """
        Usage:
          huddle auth-token --show | --rotate
          huddle dashboard [--json]
          huddle devices list [--url http://127.0.0.1:PORT]
          huddle devices approve [REQUEST_ID] [--scope SCOPE ...] [--url URL]
          huddle devices reject REQUEST_ID [--url URL]
          huddle devices revoke|remove|rotate --device DEVICE_ID [--url URL]
          huddle stop
        """;

    /// <summary>Whether the first argument is a verb word: present, with no <c>-</c> start and no <c>=</c> (so <c>--urls=...</c> still starts the server).</summary>
    /// <param name="args">The command-line arguments.</param>
    public static bool Handles(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        return args.Length > 0
            && !string.IsNullOrEmpty(args[0])
            && args[0][0] != '-'
            && !args[0].Contains('=');
    }

    /// <summary>Runs a verb and returns the process exit code: 0 done, 1 failed or refused, 2 usage.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="context">The seams.</param>
    /// <param name="ct">Cancels the call.</param>
    public static async Task<int> RunAsync(string[] args, CliContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(context);

        string verb = args.Length > 0 ? args[0] : string.Empty;
        string[] rest = [.. args.Skip(1)];
        return verb switch
        {
            "auth-token" => await AuthTokenAsync(rest, context).ConfigureAwait(false),
            "dashboard" => await DashboardAsync(rest, context, ct).ConfigureAwait(false),
            "devices" => await CliDevices.RunAsync(rest, context, ct).ConfigureAwait(false),
            _ => await PrintUsageAsync(context).ConfigureAwait(false),
        };
    }

    /// <summary>Prints the usage to standard error.</summary>
    /// <param name="context">The seams.</param>
    internal static async Task<int> PrintUsageAsync(CliContext context)
    {
        await context.Console.Error.WriteLineAsync(Usage).ConfigureAwait(false);
        return 2;
    }

    /// <summary>The running desktop copy: its record when the instance lock is held, otherwise <see langword="null"/>.</summary>
    /// <param name="dataDir">The data directory.</param>
    internal static InstanceInfo? RunningCopy(string dataDir)
    {
        using (InstanceLock? probe = InstanceLock.TryAcquire(dataDir))
        {
            if (probe is not null)
            {
                return null;
            }
        }

        return InstanceFile.TryRead(dataDir);
    }

    private static async Task<int> AuthTokenAsync(string[] options, CliContext context)
    {
        if (options is ["--show"])
        {
            return await ShowTokenAsync(context).ConfigureAwait(false);
        }

        if (options is ["--rotate"])
        {
            _ = new GatewaySettingsStore(context.DataDir).RotateToken();
            await context.Console.Out.WriteLineAsync(
                "The stored gateway token was replaced. Restart Huddle for it to take effect; read it with 'huddle auth-token --show' in a terminal.").ConfigureAwait(false);
            return 0;
        }

        return await PrintUsageAsync(context).ConfigureAwait(false);
    }

    private static async Task<int> ShowTokenAsync(CliContext context)
    {
        if (context.Console.IsOutputRedirected)
        {
            await context.Console.Error.WriteLineAsync(
                "Refusing to print the token: standard output is redirected (this is also true under mintty / Git Bash). Run 'huddle auth-token --show' in a plain terminal such as PowerShell or cmd.").ConfigureAwait(false);
            return 1;
        }

        string? token = await ResolveTokenAsync(context).ConfigureAwait(false);
        if (token is null)
        {
            return 1;
        }

        await context.Console.Out.WriteLineAsync(token).ConfigureAwait(false);
        return 0;
    }

    /// <summary>The effective gateway token (configuration, then environment, then stored), or <see langword="null"/> after printing why there is none.</summary>
    /// <param name="context">The seams.</param>
    internal static async Task<string?> ResolveTokenAsync(CliContext context)
    {
        try
        {
            return GatewaySettings.ReadForStartup(context.DataDir, context.Configuration, context.Environment).Token;
        }
        catch (InvalidOperationException ex)
        {
            await context.Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
            return null;
        }
    }

    private static async Task<int> DashboardAsync(string[] options, CliContext context, CancellationToken ct)
    {
        bool json = options is ["--json"];
        if (!json && options.Length > 0)
        {
            return await PrintUsageAsync(context).ConfigureAwait(false);
        }

        InstanceInfo? info = RunningCopy(context.DataDir);
        if (info is null)
        {
            await context.Console.Error.WriteLineAsync("No desktop copy is running.").ConfigureAwait(false);
            return 1;
        }

        string? url = await DesktopBootstrapClient.RequestUrlAsync(info, context.Handler, ct).ConfigureAwait(false);
        if (url is null)
        {
            await context.Console.Error.WriteLineAsync("The running copy did not hand out a sign-in link (it may be an older version, or it did not answer).").ConfigureAwait(false);
            return 1;
        }

        if (json)
        {
            await context.Console.Out.WriteLineAsync(JsonSerializer.Serialize(new { url })).ConfigureAwait(false);
            return 0;
        }

        context.Opener.Open(url);
        await context.Console.Out.WriteLineAsync("Opened Huddle in your browser.").ConfigureAwait(false);
        return 0;
    }
}
