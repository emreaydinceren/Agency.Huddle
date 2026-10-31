using System.Net;
using System.Net.Sockets;

namespace Agency.Huddle.App.Gateway;

/// <summary>The plain-HTTP rule of Spec 8.8 with correction D-10.</summary>
internal static class GatewayPlainHttp
{
    /// <summary>
    /// Whether a request may use <paramref name="scheme"/> with <paramref name="host"/>: https always; http only for <c>localhost</c>, loopback and
    /// private IP literals (including <c>100.64.0.0/10</c>), <c>*.local</c>, <c>*.ts.net</c>, and the hosts named in <c>ExtraHosts</c> or <c>CustomBindHost</c>.
    /// </summary>
    /// <param name="scheme">The effective request scheme.</param>
    /// <param name="host">The host without its port (an IPv6 literal bracketed).</param>
    /// <param name="options">The gateway options.</param>
    public static bool IsAllowed(string scheme, string host, GatewayOptions options)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(options);

        if (string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string name = host.Trim();
        if (name.Length == 0)
        {
            return false;
        }

        bool named = string.Equals(options.CustomBindHost?.Trim(), name, StringComparison.OrdinalIgnoreCase)
            || options.ExtraHosts.Any(extra => string.Equals(extra.Trim(), name, StringComparison.OrdinalIgnoreCase));
        return named || IsLocalOrPrivateHost(name);
    }

    /// <summary>Whether <paramref name="host"/> is a loopback name, a loopback or private IP literal, or ends in <c>.local</c> or <c>.ts.net</c>.</summary>
    /// <param name="host">The host without its port (an IPv6 literal may be bracketed).</param>
    internal static bool IsLocalOrPrivateHost(string host)
    {
        string name = host.Trim('[', ']');
        if (ConnectEndpoints.IsLoopbackName(name))
        {
            return true;
        }

        if (IPAddress.TryParse(name, out IPAddress? address))
        {
            return IsPrivate(address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address);
        }

        return name.EndsWith(".local", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".ts.net", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPrivate(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal || (bytes[0] & 0xFE) == 0xFC;
        }

        return bytes[0] == 10
            || (bytes[0] == 172 && (bytes[1] & 0xF0) == 16)
            || (bytes[0] == 192 && bytes[1] == 168)
            || (bytes[0] == 169 && bytes[1] == 254)
            || (bytes[0] == 100 && (bytes[1] & 0xC0) == 0x40);
    }
}
