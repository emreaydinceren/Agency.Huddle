using System.ComponentModel;
using System.Net;
using System.Text.Json;

namespace Agency.Huddle.App.Gateway;

/// <summary>The local node as <c>tailscale status --json</c> reports it (Spec 6.3).</summary>
/// <param name="State">The backend state.</param>
/// <param name="DnsName">The MagicDNS name without its trailing dot, or <see langword="null"/>.</param>
/// <param name="Addresses">The node's Tailscale addresses.</param>
/// <param name="OwnerLogin">The node owner's login name, or <see langword="null"/>.</param>
internal sealed record TailscaleStatus(TailscaleState State, string? DnsName, IReadOnlyList<IPAddress> Addresses, string? OwnerLogin)
{
    private const int Attempts = 3;
    private static readonly TimeSpan AttemptGap = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(10);
    private static readonly string[] StatusArgs = ["status", "--json"];

    /// <summary>Parses the JSON of <c>tailscale status --json</c>; anything unreadable is <see cref="TailscaleState.NotRunning"/>.</summary>
    /// <param name="json">The command output.</param>
    public static TailscaleStatus Parse(string json)
    {
        TailscaleStatus unreadable = new(TailscaleState.NotRunning, null, [], null);
        if (string.IsNullOrWhiteSpace(json))
        {
            return unreadable;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("BackendState", out JsonElement backend)
                || backend.ValueKind != JsonValueKind.String)
            {
                return unreadable;
            }

            TailscaleState state = backend.GetString() switch
            {
                "Running" => TailscaleState.Running,
                "NeedsLogin" => TailscaleState.NeedsLogin,
                "Stopped" => TailscaleState.Stopped,
                _ => TailscaleState.NotRunning,
            };
            return ReadSelf(root, state);
        }
        catch (JsonException)
        {
            return unreadable;
        }
    }

    /// <summary>Runs <c>status --json</c> up to 3 times, 500 ms apart, with a 10 s timeout per call.</summary>
    /// <param name="cli">The CLI.</param>
    /// <param name="timeProvider">The clock for the waits, or <see langword="null"/> for the system clock.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public static async Task<TailscaleStatus> ReadAsync(ITailscaleCli cli, TimeProvider? timeProvider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cli);
        TimeProvider clock = timeProvider ?? TimeProvider.System;
        TailscaleStatus last = new(TailscaleState.NotRunning, null, [], null);
        for (int attempt = 1; attempt <= Attempts; attempt++)
        {
            try
            {
                (int exit, string stdout, _) = await cli.RunAsync(StatusArgs, CallTimeout, cancellationToken).ConfigureAwait(false);
                if (exit == 0)
                {
                    last = Parse(stdout);
                    if (last.State != TailscaleState.NotRunning)
                    {
                        return last;
                    }
                }
            }
            catch (Win32Exception)
            {
                return new TailscaleStatus(TailscaleState.Missing, null, [], null);
            }
            catch (TimeoutException)
            {
                // A slow daemon counts as a failed attempt; the next attempt may succeed.
            }

            if (attempt < Attempts)
            {
                await Task.Delay(AttemptGap, clock, cancellationToken).ConfigureAwait(false);
            }
        }

        return last;
    }

    private static TailscaleStatus ReadSelf(JsonElement root, TailscaleState state)
    {
        string? dns = null;
        List<IPAddress> addresses = [];
        string? owner = null;
        if (root.TryGetProperty("Self", out JsonElement self) && self.ValueKind == JsonValueKind.Object)
        {
            if (self.TryGetProperty("DNSName", out JsonElement name) && name.ValueKind == JsonValueKind.String)
            {
                dns = name.GetString()?.TrimEnd('.');
            }

            if (self.TryGetProperty("TailscaleIPs", out JsonElement ips) && ips.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement ip in ips.EnumerateArray())
                {
                    if (ip.ValueKind == JsonValueKind.String && IPAddress.TryParse(ip.GetString(), out IPAddress? parsed))
                    {
                        addresses.Add(parsed);
                    }
                }
            }

            if (self.TryGetProperty("UserID", out JsonElement userId)
                && root.TryGetProperty("User", out JsonElement users)
                && users.ValueKind == JsonValueKind.Object
                && users.TryGetProperty(userId.ToString(), out JsonElement user)
                && user.TryGetProperty("LoginName", out JsonElement login)
                && login.ValueKind == JsonValueKind.String)
            {
                owner = login.GetString();
            }
        }

        return new TailscaleStatus(state, string.IsNullOrEmpty(dns) ? null : dns, addresses, owner);
    }
}
