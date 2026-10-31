using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.App.Cli;

/// <summary>Prints the development-run ready line with <c>Console.Out</c>, never through <c>ILogger</c> (Spec 6.9, C-14).</summary>
internal static class DevConsole
{
    /// <summary>The line prefix.</summary>
    public const string Prefix = "Huddle is ready: ";

    /// <summary>
    /// Writes <c>Huddle is ready: {first address}/auth?return=%2F#bootstrapToken=...</c> when there is an address, nothing otherwise.
    /// </summary>
    /// <param name="addresses">What <c>IServerAddressesFeature</c> reports, or <see langword="null"/>.</param>
    /// <param name="links">Mints the token.</param>
    /// <param name="output">Where the line goes.</param>
    /// <returns>Whether a line was written.</returns>
    public static bool Announce(IEnumerable<string>? addresses, BootstrapLinks links, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(output);

        string? first = addresses?.FirstOrDefault(address => !string.IsNullOrWhiteSpace(address));
        if (first is null)
        {
            return false;
        }

        output.WriteLine(Prefix + links.AdminUrl(first));
        return true;
    }
}
