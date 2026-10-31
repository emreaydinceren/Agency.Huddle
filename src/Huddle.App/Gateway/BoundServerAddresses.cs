using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace Agency.Huddle.App.Gateway;

/// <summary>
/// The addresses a server really listens on. The in-memory <c>TestServer</c> binds nothing, yet the host copies configured <c>urls</c> and
/// <c>http_ports</c> (the SDK container sets <c>ASPNETCORE_HTTP_PORTS</c>) into its address feature, so that feature is no evidence of a listener there.
/// </summary>
internal static class BoundServerAddresses
{
    private const string TestServerTypeName = "Microsoft.AspNetCore.TestHost.TestServer";

    /// <summary>The addresses the server reports as bound; empty for a <c>TestServer</c> or a server without an address feature.</summary>
    /// <param name="server">The web server.</param>
    internal static IReadOnlyList<string> Of(IServer server)
    {
        ArgumentNullException.ThrowIfNull(server);

        if (string.Equals(server.GetType().FullName, TestServerTypeName, StringComparison.Ordinal))
        {
            return [];
        }

        IServerAddressesFeature? feature = server.Features.Get<IServerAddressesFeature>();
        return feature is null ? [] : [.. feature.Addresses.Where(static address => !string.IsNullOrWhiteSpace(address))];
    }
}
