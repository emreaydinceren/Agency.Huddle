using System.ComponentModel;
using System.Net;
using System.Text.Json;

namespace Agency.Huddle.App.Gateway;

/// <summary><c>tailscale whois --json {ip}</c> as the <see cref="IIngressWhois"/> seam (Spec 6.3): cached 60 s per address, failures 5 s.</summary>
/// <param name="cli">The CLI.</param>
/// <param name="timeProvider">The clock, or <see langword="null"/> for the system clock.</param>
internal sealed class TailscaleWhois(ITailscaleCli cli, TimeProvider? timeProvider = null) : IIngressWhois
{
    private static readonly TimeSpan Success = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan Failure = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(10);

    private readonly ITailscaleCli cli = cli;
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;
    private readonly Lock gate = new();
    private readonly Dictionary<IPAddress, (string? Login, DateTimeOffset Expires)> cache = [];

    /// <summary>The login in a <c>whois</c> answer; <see langword="null"/> for a tagged node (no identity) or unreadable input.</summary>
    /// <param name="json">The command output.</param>
    public static string? ParseLogin(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (root.TryGetProperty("Node", out JsonElement node)
                && node.ValueKind == JsonValueKind.Object
                && node.TryGetProperty("Tags", out JsonElement tags)
                && tags.ValueKind == JsonValueKind.Array
                && tags.GetArrayLength() > 0)
            {
                return null;
            }

            return root.TryGetProperty("UserProfile", out JsonElement profile)
                && profile.ValueKind == JsonValueKind.Object
                && profile.TryGetProperty("LoginName", out JsonElement login)
                && login.ValueKind == JsonValueKind.String
                && login.GetString() is { Length: > 0 } name
                    ? name
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async ValueTask<string?> LookupLoginAsync(IPAddress client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        IPAddress key = client.IsIPv4MappedToIPv6 ? client.MapToIPv4() : client;
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        lock (this.gate)
        {
            if (this.cache.TryGetValue(key, out (string? Login, DateTimeOffset Expires) hit) && hit.Expires > now)
            {
                return hit.Login;
            }
        }

        string? login = null;
        TimeSpan ttl = Failure;
        try
        {
            (int exit, string stdout, _) = await this.cli.RunAsync(["whois", "--json", key.ToString()], CallTimeout, cancellationToken).ConfigureAwait(false);
            if (exit == 0)
            {
                login = ParseLogin(stdout);
                ttl = Success;
            }
        }
        catch (Win32Exception)
        {
            // No CLI: no identity, retried after the failure window.
        }
        catch (TimeoutException)
        {
            // A slow daemon: no identity, retried after the failure window.
        }

        lock (this.gate)
        {
            this.cache[key] = (login, this.timeProvider.GetUtcNow() + ttl);
        }

        return login;
    }
}
