using System.Net;
using System.Net.Sockets;

namespace Agency.Huddle.App.Gateway;

/// <summary>
/// Mints bootstrap tokens and builds the URLs that carry them (Spec 6.9, C-9). Every URL is
/// <c>{origin}/auth?return=%2F#bootstrapToken={token}</c>: the token is in the fragment, so no server log sees it.
/// </summary>
/// <param name="store">The token store.</param>
internal sealed class BootstrapLinks(BootstrapTokenStore store)
{
    private static readonly string[] AnyAddressHosts = ["[::]", "[::0]", "0.0.0.0", "+", "*"];

    private readonly BootstrapTokenStore store = store;

    /// <summary>The path, query and fragment name every bootstrap URL carries.</summary>
    public const string Suffix = "/auth?return=%2F#bootstrapToken=";

    /// <summary>Mints an admin bootstrap token and returns the URL for <paramref name="baseUrl"/> (an any-address is mapped to 127.0.0.1).</summary>
    /// <param name="baseUrl">The listening address, for example <c>http://127.0.0.1:5123</c> or <c>http://[::]:5123</c>.</param>
    public string AdminUrl(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        return NormalizeListenAddress(baseUrl) + Suffix + this.store.Issue(BootstrapTokenStore.Kind.Admin);
    }

    /// <summary>
    /// Mints a setup-link (limited) token and returns the URL for <paramref name="origin"/>, or a refusal when a non-loopback origin is plain
    /// <c>http</c> and the host is not a private IP literal, <c>.local</c> or <c>.ts.net</c> (no token is minted then).
    /// </summary>
    /// <param name="origin">The origin the other device will open, for example <c>https://desktop-a.tail1234.ts.net:5101</c>.</param>
    public SetupLinkResult SetupLink(string origin)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(origin);

        string trimmed = origin.TrimEnd('/');
        return IsSetupLinkOriginAllowed(trimmed)
            ? new SetupLinkResult(true, trimmed + Suffix + this.store.Issue(BootstrapTokenStore.Kind.SetupLink), null)
            : new SetupLinkResult(false, null, "A setup link over plain http is allowed only for a private network address, a .local name or a .ts.net name; use https.");
    }

    /// <summary>Maps <c>0.0.0.0</c>, <c>[::]</c>, <c>+</c> and <c>*</c> hosts to <c>127.0.0.1</c>, keeps the scheme and port, and drops a trailing slash.</summary>
    /// <param name="listenUrl">A Kestrel address.</param>
    public static string NormalizeListenAddress(string listenUrl)
    {
        ArgumentNullException.ThrowIfNull(listenUrl);

        int schemeEnd = listenUrl.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd < 0)
        {
            return listenUrl.TrimEnd('/');
        }

        string scheme = listenUrl[..schemeEnd];
        string rest = listenUrl[(schemeEnd + 3)..];
        int slash = rest.IndexOf('/', StringComparison.Ordinal);
        string authority = slash < 0 ? rest : rest[..slash];
        int colon = authority.LastIndexOf(':');
        bool hasPort = colon > authority.LastIndexOf(']');
        string host = hasPort ? authority[..colon] : authority;
        string port = hasPort ? authority[colon..] : string.Empty;
        string mapped = AnyAddressHosts.Contains(host, StringComparer.Ordinal) ? "127.0.0.1" : host;
        return $"{scheme}://{mapped}{port}";
    }

    /// <summary>Whether a setup link may point at <paramref name="origin"/> (the https rule, Spec 6.9).</summary>
    /// <param name="origin">The origin.</param>
    public static bool IsSetupLinkOriginAllowed(string origin)
    {
        ArgumentNullException.ThrowIfNull(origin);

        if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && IsPlainHttpHostAllowed(uri.Host);
    }

    private static bool IsPlainHttpHostAllowed(string host)
    {
        if (host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".ts.net", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(host.Trim('[', ']'), out IPAddress? address))
        {
            return false;
        }

        if (IPAddress.IsLoopback(address) || address.IsIPv6UniqueLocal || address.IsIPv6LinkLocal)
        {
            return true;
        }

        byte[] bytes = address.GetAddressBytes();
        return address.AddressFamily == AddressFamily.InterNetwork
            && (bytes[0] == 10 || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) || (bytes[0] == 192 && bytes[1] == 168));
    }

    /// <summary>The outcome of <see cref="SetupLink"/>.</summary>
    /// <param name="Allowed">Whether a token was minted.</param>
    /// <param name="Url">The link when allowed.</param>
    /// <param name="Reason">Why it was refused.</param>
    public sealed record SetupLinkResult(bool Allowed, string? Url, string? Reason);
}
