using Agency.Huddle.App.Cli;
using Agency.Huddle.App.Gateway;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Spec 6.9 and C-14: a development run prints one ready line through the console writer, only when the server reports an address.</summary>
public sealed class DevConsoleTests
{
    private static BootstrapLinks Links() => new(new BootstrapTokenStore(new FakeTimeProvider(new DateTimeOffset(2026, 10, 30, 9, 0, 0, TimeSpan.Zero))));

    /// <summary>The line is <c>Huddle is ready: {first address}/auth?return=%2F#bootstrapToken=...</c>, one line.</summary>
    [Fact]
    public void Announce_WithAddresses_WritesOneReadyLineForTheFirst()
    {
        StringWriter output = new();

        bool written = DevConsole.Announce(["http://localhost:5100", "https://localhost:5101"], Links(), output);

        Assert.True(written);
        string[] lines = output.ToString().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        string line = Assert.Single(lines);
        Assert.Matches(@"^Huddle is ready: http://localhost:5100/auth\?return=%2F#bootstrapToken=[A-Za-z0-9_-]{43}$", line);    }

    /// <summary>An any-address is mapped to 127.0.0.1 in the printed URL.</summary>
    [Fact]
    public void Announce_AnyAddress_PrintsLoopback()
    {
        StringWriter output = new();

        _ = DevConsole.Announce(["http://[::]:5100"], Links(), output);

        Assert.Matches(@"^Huddle is ready: http://127\.0\.0\.1:5100/auth\?return=%2F#bootstrapToken=[A-Za-z0-9_-]{43}\r?\n$", output.ToString());
    }

    /// <summary>No address, or no feature at all, prints nothing (a TestServer run stays quiet).</summary>
    /// <param name="none">Whether to pass <see langword="null"/> rather than an empty list.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Announce_NoAddresses_WritesNothing(bool none)
    {
        StringWriter output = new();

        bool written = DevConsole.Announce(none ? null : [], Links(), output);

        Assert.False(written);
        Assert.Empty(output.ToString());
    }
}
