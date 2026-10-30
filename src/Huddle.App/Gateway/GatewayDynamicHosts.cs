using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Agency.Huddle.App.Gateway;

/// <summary>
/// The dynamic part of the host filter's allowed list (Spec 6.4; D-7): <c>PostConfigure&lt;HostFilteringOptions&gt;</c> appends <see cref="Current"/> to the
/// configured <c>AllowedHosts</c>, and <see cref="Set"/> signals the options change token.
/// </summary>
internal sealed class GatewayDynamicHosts : IOptionsChangeTokenSource<HostFilteringOptions>, IDisposable
{
    private readonly Lock gate = new();

    private IReadOnlyList<string> current = [];
    private CancellationTokenSource changed = new();

    /// <summary>The dynamic host names now in force.</summary>
    public IReadOnlyList<string> Current
    {
        get
        {
            lock (this.gate)
            {
                return this.current;
            }
        }
    }

    /// <summary>The default options name: the host filter reads the unnamed options.</summary>
    public string Name => Options.DefaultName;

    /// <summary>Replaces the dynamic hosts and fires the options change token.</summary>
    /// <param name="hosts">The hosts (a default port stripped by the filter's comparison).</param>
    public void Set(IEnumerable<string> hosts)
    {
        ArgumentNullException.ThrowIfNull(hosts);

        CancellationTokenSource previous;
        lock (this.gate)
        {
            this.current = [.. hosts];
            previous = this.changed;
            this.changed = new CancellationTokenSource();
        }

        previous.Cancel();
        previous.Dispose();
    }

    /// <inheritdoc />
    public void Dispose() => this.changed.Dispose();

    /// <inheritdoc />
    public IChangeToken GetChangeToken()
    {
        lock (this.gate)
        {
            return new CancellationChangeToken(this.changed.Token);
        }
    }

    /// <summary>
    /// The automatic host list (D-10): the bound non-loopback addresses (IPv6 bracketed, no zone), <c>{machine}.local</c> (never the bare machine name),
    /// <c>CustomBindHost</c> and <c>ExtraHosts</c>.
    /// </summary>
    /// <param name="options">The gateway options.</param>
    /// <param name="machineName">The machine name.</param>
    /// <param name="boundAddresses">The non-loopback unicast addresses of an any-address bind.</param>
    public static IReadOnlyList<string> Automatic(GatewayOptions options, string machineName, IReadOnlyList<IPAddress> boundAddresses)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(boundAddresses);

        List<string> hosts = [];
        foreach (IPAddress address in boundAddresses)
        {
            hosts.Add(address.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{new IPAddress(address.GetAddressBytes())}]" : address.ToString());
        }

        if (!string.IsNullOrWhiteSpace(machineName))
        {
            hosts.Add($"{machineName.Trim().ToLowerInvariant()}.local");
        }

        if (!string.IsNullOrWhiteSpace(options.CustomBindHost))
        {
            hosts.Add(options.CustomBindHost.Trim());
        }

        hosts.AddRange(options.ExtraHosts.Where(static host => !string.IsNullOrWhiteSpace(host)).Select(static host => host.Trim()));
        return [.. hosts.Distinct(StringComparer.OrdinalIgnoreCase)];
    }
}
