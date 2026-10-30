using Agency.Huddle.App.Cli;
using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Spec 6.13 and C-14: <c>auth-token --show</c> / <c>--rotate</c>, and the usage rule for an unknown first argument.</summary>
public sealed class CliAuthTokenAndUsageTests
{
    /// <summary>On a terminal, <c>--show</c> prints the resolved token and exits 0.</summary>
    [Fact]
    public async Task Show_Terminal_PrintsTheToken()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CliHarness harness = new();

        int code = await harness.RunAsync(null, ct, "auth-token", "--show");

        Assert.Equal(0, code);
        Assert.Equal(CliHarness.Token + Environment.NewLine, harness.Console.OutText);
    }

    /// <summary>Redirected output is refused with exit 1, a message that names mintty and Git Bash, and no token anywhere.</summary>
    [Fact]
    public async Task Show_OutputRedirected_RefusesAndMentionsMintty()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CliHarness harness = new();
        harness.Console.IsOutputRedirected = true;

        int code = await harness.RunAsync(null, ct, "auth-token", "--show");

        Assert.Equal(1, code);
        Assert.Equal(
            "Refusing to print the token: standard output is redirected (this is also true under mintty / Git Bash). Run 'huddle auth-token --show' in a plain terminal such as PowerShell or cmd." + Environment.NewLine,
            harness.Console.ErrorText);
        Assert.Empty(harness.Console.OutText);
        Assert.DoesNotContain(CliHarness.Token, harness.Console.AllText, StringComparison.Ordinal);
    }

    /// <summary>With no configured token and no environment variable, <c>--show</c> prints the stored one (generated on first use).</summary>
    [Fact]
    public async Task Show_StoredToken_PrintsTheStoredValue()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CliHarness harness = new() { ConfigToken = false };

        int code = await harness.RunAsync(null, ct, "auth-token", "--show");

        Assert.Equal(0, code);
        string? stored = new GatewaySettingsStore(harness.DataDir).StoredToken;
        Assert.NotNull(stored);
        Assert.Equal(stored + Environment.NewLine, harness.Console.OutText);
    }

    /// <summary>The environment variable is the token when no configuration supplies one.</summary>
    [Fact]
    public async Task Show_EnvironmentToken_PrintsIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CliHarness harness = new() { ConfigToken = false };
        _ = harness.Environment.With(GatewaySettings.TokenEnvironmentVariable, "env-token-0123456789abcdef");

        int code = await harness.RunAsync(null, ct, "auth-token", "--show");

        Assert.Equal(0, code);
        Assert.Equal("env-token-0123456789abcdef" + Environment.NewLine, harness.Console.OutText);
    }

    /// <summary><c>--rotate</c> replaces the stored token, exits 0 and does not print the new value.</summary>
    [Fact]
    public async Task Rotate_ReplacesTheStoredTokenWithoutPrintingIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CliHarness harness = new() { ConfigToken = false };
        string before = new GatewaySettingsStore(harness.DataDir).EnsureToken();

        int code = await harness.RunAsync(null, ct, "auth-token", "--rotate");

        Assert.Equal(0, code);
        string? after = new GatewaySettingsStore(harness.DataDir).StoredToken;
        Assert.NotNull(after);
        Assert.NotEqual(before, after);
        Assert.DoesNotContain(after, harness.Console.AllText, StringComparison.Ordinal);
    }

    /// <summary><c>auth-token</c> with no option, or an unknown one, is a usage error (exit 2).</summary>
    /// <param name="option">The option, or an empty string for none.</param>
    [Theory]
    [InlineData("")]
    [InlineData("--bogus")]
    public async Task AuthToken_NoKnownOption_IsUsageExit2(string option)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CliHarness harness = new();
        string[] args = string.IsNullOrEmpty(option) ? ["auth-token"] : ["auth-token", option];

        int code = await harness.RunAsync(null, ct, args);

        Assert.Equal(2, code);
        Assert.Equal(CliVerbs.Usage + Environment.NewLine, harness.Console.ErrorText);
    }

    /// <summary>An unknown first argument with no <c>-</c> and no <c>=</c> prints usage naming the verbs and exits 2, instead of starting the server.</summary>
    [Fact]
    public async Task Run_UnknownVerb_PrintsUsageAndExits2()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CliHarness harness = new();

        int code = await harness.RunAsync(null, ct, "bogus");

        Assert.Equal(2, code);
        Assert.Equal(CliVerbs.Usage + Environment.NewLine, harness.Console.ErrorText);
        Assert.Empty(harness.Console.OutText);
    }

    /// <summary>Which first arguments the verb runner takes over: a bare word does; options, <c>key=value</c> and nothing do not.</summary>
    /// <param name="first">The first argument, or <see langword="null"/> for no arguments.</param>
    /// <param name="handles">Whether <see cref="CliVerbs.Handles"/> says yes.</param>
    [Theory]
    [InlineData("devices", true)]
    [InlineData("auth-token", true)]
    [InlineData("dashboard", true)]
    [InlineData("bogus", true)]
    [InlineData("--urls=http://127.0.0.1:0", false)]
    [InlineData("--help", false)]
    [InlineData("Team:DataDir=x", false)]
    [InlineData(null, false)]
    public void Handles_OnlyABareFirstWord(string? first, bool handles)
    {
        string[] args = first is null ? [] : [first, "list"];

        Assert.Equal(handles, CliVerbs.Handles(args));
    }
}
