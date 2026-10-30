using System.Net;

namespace Agency.Huddle.App.Gateway;

/// <summary>The facts the ingress classifier reads from one request.</summary>
/// <param name="Listener">Which listener accepted it.</param>
/// <param name="SocketAddress">The remote socket address, or <see langword="null"/> (in-memory server, pipe, Unix socket).</param>
/// <param name="Headers">The request headers, with case-insensitive names.</param>
internal sealed record IngressRequest(
    IngressListenerKind Listener,
    IPAddress? SocketAddress,
    IReadOnlyDictionary<string, string> Headers);
