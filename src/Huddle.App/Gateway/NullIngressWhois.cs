using System.Net;

namespace Agency.Huddle.App.Gateway;

/// <summary>The whois used until the Tailscale controller exists: it knows no one, so no Tailscale identity is ever verified.</summary>
internal sealed class NullIngressWhois : IIngressWhois
{
    /// <inheritdoc />
    public ValueTask<string?> LookupLoginAsync(IPAddress client, CancellationToken cancellationToken) => ValueTask.FromResult<string?>(null);
}
