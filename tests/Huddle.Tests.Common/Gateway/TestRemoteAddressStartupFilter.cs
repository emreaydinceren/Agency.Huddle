using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>
/// Gives a <c>TestServer</c> request the remote address named by the <see cref="HeaderName"/> header, so a hosted test can act as a LAN client.
/// Register it with <c>services.AddSingleton&lt;IStartupFilter, TestRemoteAddressStartupFilter&gt;()</c>; it runs before the app's middleware.
/// </summary>
internal sealed class TestRemoteAddressStartupFilter : IStartupFilter
{
    /// <summary>The header that carries the simulated remote address.</summary>
    public const string HeaderName = "X-Test-Remote-Ip";

    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return app =>
        {
            app.Use(async (context, pipeline) =>
            {
                if (context.Request.Headers.TryGetValue(HeaderName, out Microsoft.Extensions.Primitives.StringValues value)
                    && IPAddress.TryParse(value.ToString(), out IPAddress? address))
                {
                    context.Connection.RemoteIpAddress = address;
                }

                await pipeline().ConfigureAwait(false);
            });
            next(app);
        };
    }
}
