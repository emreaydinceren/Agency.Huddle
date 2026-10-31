using System.Net;
using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>An <see cref="IIngressWhois"/> with canned answers that records every lookup.</summary>
internal sealed class FakeIngressWhois : IIngressWhois
{
    private readonly Dictionary<IPAddress, string> logins = [];

    /// <summary>The addresses looked up, in order.</summary>
    public List<IPAddress> Lookups { get; } = [];

    /// <summary>Makes <paramref name="client"/> resolve to <paramref name="login"/>.</summary>
    /// <param name="client">The client address.</param>
    /// <param name="login">The Tailscale login name.</param>
    public FakeIngressWhois With(IPAddress client, string login)
    {
        this.logins[client] = login;
        return this;
    }

    /// <inheritdoc />
    public ValueTask<string?> LookupLoginAsync(IPAddress client, CancellationToken cancellationToken)
    {
        this.Lookups.Add(client);
        return ValueTask.FromResult(this.logins.TryGetValue(client, out string? login) ? login : null);
    }
}
