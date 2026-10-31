using System.Net;

namespace Agency.Huddle.App.Gateway;

/// <summary>The seam over <c>tailscale whois</c> for the classifier (Spec 6.3).</summary>
internal interface IIngressWhois
{
    /// <summary>Returns the Tailscale login name that owns <paramref name="client"/>, or <see langword="null"/>.</summary>
    /// <param name="client">The forwarded client address.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    ValueTask<string?> LookupLoginAsync(IPAddress client, CancellationToken cancellationToken);
}
