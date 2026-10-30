using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>
/// Simulates the managed listener on a <c>TestServer</c> (D-2): a request carrying the <see cref="HeaderName"/> header gets
/// <c>Connection.LocalIpAddress</c> and <c>LocalPort</c> set to <see cref="Endpoint"/>, the endpoint a test registered in its <c>GatewayListener</c>.
/// </summary>
/// <param name="endpoint">The managed endpoint to present.</param>
internal sealed class TestManagedListenerStartupFilter(IPEndPoint endpoint) : IStartupFilter
{
    /// <summary>The header that makes a request arrive on the managed listener.</summary>
    public const string HeaderName = "X-Test-Managed";

    /// <summary>The managed endpoint presented.</summary>
    public IPEndPoint Endpoint { get; } = endpoint;

    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return app =>
        {
            app.Use(async (context, pipeline) =>
            {
                if (context.Request.Headers.ContainsKey(HeaderName))
                {
                    context.Connection.LocalIpAddress = this.Endpoint.Address;
                    context.Connection.LocalPort = this.Endpoint.Port;
                }

                await pipeline().ConfigureAwait(false);
            });
            next(app);
        };
    }
}
