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
    private IReadOnlyList<string> tailscale = [];
    private CancellationTokenSource changed = new();

    // Set and SetTailscale cancel the source they swap out after leaving the gate, so Dispose takes the same gate: a
    // Tailscale poll that lands while the container disposes must not cancel a disposed source (issue 677).
    private bool disposed;

    /// <summary>The dynamic host names now in force (the automatic hosts and the Tailscale hosts).</summary>
    public IReadOnlyList<string> Current
    {
        get
        {
            lock (this.gate)
            {
                return [.. this.current, .. this.tailscale];
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
            if (this.disposed)
            {
                return;
            }

            previous = this.changed;
            this.changed = new CancellationTokenSource();
        }

        previous.Cancel();
        previous.Dispose();
    }

    /// <summary>
    /// Replaces the Tailscale part of <see cref="Current"/> (the MagicDNS name, with and without the port) and fires the change token when it differs.
    /// <see cref="Set"/> leaves this part alone and this method leaves the <see cref="Set"/> part alone.
    /// </summary>
    /// <param name="hosts">The Tailscale hosts.</param>
    public void SetTailscale(IEnumerable<string> hosts)
    {
        ArgumentNullException.ThrowIfNull(hosts);

        string[] next = [.. hosts.Distinct(StringComparer.OrdinalIgnoreCase)];
        CancellationTokenSource previous;
        lock (this.gate)
        {
            if (this.tailscale.SequenceEqual(next, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            this.tailscale = next;
            if (this.disposed)
            {
                return;
            }

            previous = this.changed;
            this.changed = new CancellationTokenSource();
        }

        previous.Cancel();
        previous.Dispose();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (this.gate)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            this.changed.Dispose();
        }
    }

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
