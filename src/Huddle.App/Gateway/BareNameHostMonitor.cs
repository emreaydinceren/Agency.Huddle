using System.Net;

namespace Agency.Huddle.App.Gateway;

/// <summary>
/// R-13: remembers that the gate admitted a request whose <c>Host</c> is a bare name (no dot, not <c>localhost</c>, not an IP literal), so the Security
/// check can say that an allowed origin is needed (Spec 8.9).
/// </summary>
internal sealed class BareNameHostMonitor
{
    private int seen;

    /// <summary>Whether a bare-name host has been admitted since start.</summary>
    public bool InUse => Volatile.Read(ref this.seen) != 0;

    /// <summary>Whether <paramref name="host"/> is a bare name.</summary>
    /// <param name="host">The host part of the <c>Host</c> header.</param>
    public static bool IsBareName(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        string name = host.Trim().Trim('[', ']');
        return name.Length > 0
            && !name.Contains('.')
            && !string.Equals(name, "localhost", StringComparison.OrdinalIgnoreCase)
            && !IPAddress.TryParse(name, out _);
    }

    /// <summary>Records <paramref name="host"/> when it is a bare name.</summary>
    /// <param name="host">The host part of the <c>Host</c> header.</param>
    public void Note(string? host)
    {
        if (IsBareName(host))
        {
            Volatile.Write(ref this.seen, 1);
        }
    }
}
