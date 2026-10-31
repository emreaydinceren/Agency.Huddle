using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Agency.Huddle.App.Gateway;

/// <summary>The real machine seam of the listener plan (D-3, D-6): an exclusive-bind probe and the tailnet address from the network interfaces.</summary>
internal sealed class SocketProbe : GatewayListenerPlan.IProbe
{
    /// <summary>The shared instance.</summary>
    public static SocketProbe Instance { get; } = new();

    /// <summary>Whether <paramref name="port"/> can be bound exclusively on every address now; the socket is disposed at once (the race is accepted).</summary>
    /// <param name="port">A non-zero port.</param>
    public bool IsPortFree(int port)
    {
        using Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { ExclusiveAddressUse = true };
        try
        {
            socket.Bind(new IPEndPoint(IPAddress.Any, port));
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    /// <summary>The first unicast address in <c>100.64.0.0/10</c>, or <see langword="null"/>.</summary>
    public IPAddress? TailnetAddress()
    {
        foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            foreach (UnicastIPAddressInformation unicast in adapter.GetIPProperties().UnicastAddresses)
            {
                byte[] bytes = unicast.Address.GetAddressBytes();
                if (bytes.Length == 4 && bytes[0] == 100 && (bytes[1] & 0xC0) == 0x40)
                {
                    return unicast.Address;
                }
            }
        }

        return null;
    }
}
