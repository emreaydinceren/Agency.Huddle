using System.Net;
using System.Net.Sockets;

namespace Agency.Huddle.App.Gateway;

/// <summary>The stable loopback port of desktop mode (C-11): the decision only; Stage D binds it.</summary>
internal static class DesktopPortChoice
{
    private const int MaxPort = 65535;

    /// <summary>The port to request on this start: <paramref name="stored"/> when it is a valid port and free, else 0 (any port).</summary>
    /// <param name="stored">The port stored by the last start, or <see langword="null"/>.</param>
    /// <param name="isFree">Whether a port can be bound.</param>
    public static int Choose(int? stored, Func<int, bool> isFree)
    {
        ArgumentNullException.ThrowIfNull(isFree);

        return stored is { } port && port is >= 1 and <= MaxPort && isFree(port) ? port : 0;
    }

    /// <summary>Whether a loopback TCP listener can bind <paramref name="port"/> now.</summary>
    /// <param name="port">The port.</param>
    public static bool IsFree(int port)
    {
        TcpListener listener = new(IPAddress.Loopback, port);
        try
        {
            listener.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>Stores <paramref name="boundPort"/> when it differs from what is stored.</summary>
    /// <param name="store">The settings store.</param>
    /// <param name="boundPort">The port Kestrel actually bound.</param>
    public static void Remember(GatewaySettingsStore store, int boundPort)
    {
        ArgumentNullException.ThrowIfNull(store);

        if (store.DesktopPort != boundPort)
        {
            store.SetDesktopPort(boundPort);
        }
    }
}
