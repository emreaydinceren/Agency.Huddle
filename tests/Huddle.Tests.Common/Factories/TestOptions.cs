using Microsoft.Extensions.Options;
using Agency.Huddle.App;

namespace Agency.Huddle.Tests.Factories;

/// <summary>Builds <see cref="TeamOptions"/> wrappers for tests, so no test spells <c>Options.Create(new TeamOptions { DataDir = ... })</c> itself.</summary>
internal static class TestOptions
{
    /// <summary>Builds options that keep every default, including <see cref="TeamOptions.DataDir"/> (some tests assert the default paths).</summary>
    /// <param name="configure">Changes any option; <see langword="null"/> changes nothing.</param>
    public static IOptions<TeamOptions> Create(Action<TeamOptions>? configure = null)
    {
        TeamOptions options = new();
        configure?.Invoke(options);
        return Microsoft.Extensions.Options.Options.Create(options);
    }

    /// <summary>Builds options whose <see cref="TeamOptions.DataDir"/> is <paramref name="dir"/>, then applies <paramref name="configure"/>.</summary>
    /// <param name="dir">The temporary data directory the options point at.</param>
    /// <param name="configure">Changes any other option; <see langword="null"/> changes nothing.</param>
    public static IOptions<TeamOptions> For(TempDataDir dir, Action<TeamOptions>? configure = null) => dir.Options(configure);
}
