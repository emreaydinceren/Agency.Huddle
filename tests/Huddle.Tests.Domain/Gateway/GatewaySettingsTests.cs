using System.Text.RegularExpressions;
using Agency.Huddle.App.Gateway;
using Microsoft.Extensions.Configuration;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Spec 6.1: <c>GatewaySettings.ReadForStartup</c> resolves the token and password and binds <c>Team:Gateway</c> with no DI.</summary>
public sealed partial class GatewaySettingsTests
{
    private const string ConfiguredToken = "configured-token-0123456789abcdef";
    private const string EnvironmentToken = "environment-token-0123456789abcd";

    [GeneratedRegex("^[0-9a-f]{48}$", RegexOptions.CultureInvariant)]
    private static partial Regex Token48Hex();

    [GeneratedRegex("^[0-9a-f]{8}$", RegexOptions.CultureInvariant)]
    private static partial Regex InstanceId8Hex();

    /// <summary>Builds a configuration root from key/value pairs.</summary>
    private static IConfigurationRoot Config(params (string Key, string Value)[] pairs) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build();

    /// <summary>With nothing supplied, first start generates a 48-hex token, stores it, and reports it as stored.</summary>
    [Fact]
    public void ReadForStartup_FirstStart_GeneratesAndStoresToken()
    {
        using TempDataDir dir = new();

        GatewaySettings settings = GatewaySettings.ReadForStartup(dir.Path, Config(), new FakeGatewayEnvironment());

        Assert.Matches(Token48Hex(), settings.Token);
        Assert.Equal(GatewaySecretSource.Stored, settings.TokenSource);
        Assert.Equal(settings.Token, new GatewaySettingsStore(dir.Path).StoredToken);
    }

    /// <summary>A second start reuses the stored token and the same Instance id.</summary>
    [Fact]
    public void ReadForStartup_SecondStart_ReusesTokenAndInstanceId()
    {
        using TempDataDir dir = new();
        GatewaySettings first = GatewaySettings.ReadForStartup(dir.Path, Config(), new FakeGatewayEnvironment());

        GatewaySettings second = GatewaySettings.ReadForStartup(dir.Path, Config(), new FakeGatewayEnvironment());

        Assert.Equal(first.Token, second.Token);
        Assert.Equal(first.InstanceId, second.InstanceId);
        Assert.Matches(InstanceId8Hex(), second.InstanceId);
    }

    /// <summary>The token in configuration wins over the environment variable and the stored token.</summary>
    [Fact]
    public void ReadForStartup_ConfigurationToken_WinsOverEnvironmentAndStored()
    {
        using TempDataDir dir = new();
        _ = new GatewaySettingsStore(dir.Path).EnsureToken();
        FakeGatewayEnvironment environment = new FakeGatewayEnvironment().With("HUDDLE_GATEWAY_TOKEN", EnvironmentToken);

        GatewaySettings settings = GatewaySettings.ReadForStartup(dir.Path, Config(("Team:Gateway:Auth:Token", ConfiguredToken)), environment);

        Assert.Equal(ConfiguredToken, settings.Token);
        Assert.Equal(GatewaySecretSource.Configuration, settings.TokenSource);
    }

    /// <summary>With no configured token, <c>HUDDLE_GATEWAY_TOKEN</c> wins over the stored token.</summary>
    [Fact]
    public void ReadForStartup_EnvironmentToken_WinsOverStored()
    {
        using TempDataDir dir = new();
        _ = new GatewaySettingsStore(dir.Path).EnsureToken();
        FakeGatewayEnvironment environment = new FakeGatewayEnvironment().With("HUDDLE_GATEWAY_TOKEN", EnvironmentToken);

        GatewaySettings settings = GatewaySettings.ReadForStartup(dir.Path, Config(), environment);

        Assert.Equal(EnvironmentToken, settings.Token);
        Assert.Equal(GatewaySecretSource.Environment, settings.TokenSource);
    }

    /// <summary>When the environment variable is later removed, the stored token is used again.</summary>
    [Fact]
    public void ReadForStartup_EnvironmentTokenRemoved_FallsBackToStored()
    {
        using TempDataDir dir = new();
        string stored = new GatewaySettingsStore(dir.Path).EnsureToken();
        FakeGatewayEnvironment environment = new();

        GatewaySettings settings = GatewaySettings.ReadForStartup(dir.Path, Config(), environment);

        Assert.Equal(stored, settings.Token);
        Assert.Equal(GatewaySecretSource.Stored, settings.TokenSource);
    }

    /// <summary>A token supplied by configuration is not written to <c>team.db</c>: the column stays NULL.</summary>
    [Fact]
    public void ReadForStartup_ConfigurationToken_IsNotStored()
    {
        using TempDataDir dir = new();

        _ = GatewaySettings.ReadForStartup(dir.Path, Config(("Team:Gateway:Auth:Token", ConfiguredToken)), new FakeGatewayEnvironment());

        Assert.Null(new GatewaySettingsStore(dir.Path).StoredToken);
    }

    /// <summary>A token supplied by the environment is not written to <c>team.db</c> either.</summary>
    [Fact]
    public void ReadForStartup_EnvironmentToken_IsNotStored()
    {
        using TempDataDir dir = new();
        FakeGatewayEnvironment environment = new FakeGatewayEnvironment().With("HUDDLE_GATEWAY_TOKEN", EnvironmentToken);

        _ = GatewaySettings.ReadForStartup(dir.Path, Config(), environment);

        Assert.Null(new GatewaySettingsStore(dir.Path).StoredToken);
    }

    /// <summary>Startup refuses a blank token or a placeholder literal supplied by configuration.</summary>
    [Theory]
    [InlineData("   ")]
    [InlineData("undefined")]
    [InlineData("null")]
    [InlineData("__OPENCLAW_REDACTED__")]
    [InlineData("__HUDDLE_REDACTED__")]
    public void ReadForStartup_PlaceholderConfigurationToken_Throws(string token)
    {
        using TempDataDir dir = new();

        Assert.Throws<InvalidOperationException>(() =>
            GatewaySettings.ReadForStartup(dir.Path, Config(("Team:Gateway:Auth:Token", token)), new FakeGatewayEnvironment()));
    }

    /// <summary>Startup refuses a blank token or a placeholder literal supplied by the environment variable.</summary>
    [Theory]
    [InlineData("   ")]
    [InlineData("undefined")]
    [InlineData("null")]
    [InlineData("__OPENCLAW_REDACTED__")]
    public void ReadForStartup_PlaceholderEnvironmentToken_Throws(string token)
    {
        using TempDataDir dir = new();
        FakeGatewayEnvironment environment = new FakeGatewayEnvironment().With("HUDDLE_GATEWAY_TOKEN", token);

        Assert.Throws<InvalidOperationException>(() =>
            GatewaySettings.ReadForStartup(dir.Path, Config(), environment));
    }

    /// <summary><c>VerifyToken</c> accepts the token and nothing else.</summary>
    [Fact]
    public void VerifyToken_AcceptsOnlyTheToken()
    {
        using TempDataDir dir = new();
        GatewaySettings settings = GatewaySettings.ReadForStartup(dir.Path, Config(("Team:Gateway:Auth:Token", ConfiguredToken)), new FakeGatewayEnvironment());

        Assert.True(settings.VerifyToken(ConfiguredToken));
        Assert.False(settings.VerifyToken(ConfiguredToken + "x"));
        Assert.False(settings.VerifyToken(string.Empty));
    }

    /// <summary>After rotation the next start resolves the new stored token, and the old one no longer verifies.</summary>
    [Fact]
    public void Rotation_NextStart_UsesTheNewToken()
    {
        using TempDataDir dir = new();
        GatewaySettings before = GatewaySettings.ReadForStartup(dir.Path, Config(), new FakeGatewayEnvironment());
        string rotated = new GatewaySettingsStore(dir.Path).RotateToken();

        GatewaySettings after = GatewaySettings.ReadForStartup(dir.Path, Config(), new FakeGatewayEnvironment());

        Assert.Equal(rotated, after.Token);
        Assert.NotEqual(before.Token, after.Token);
        Assert.False(after.VerifyToken(before.Token));
    }

    /// <summary>Rotating the stored token does not override a token that configuration supplies.</summary>
    [Fact]
    public void Rotation_WhenConfigurationSuppliesToken_ConfigurationStillWins()
    {
        using TempDataDir dir = new();
        _ = new GatewaySettingsStore(dir.Path).RotateToken();

        GatewaySettings settings = GatewaySettings.ReadForStartup(dir.Path, Config(("Team:Gateway:Auth:Token", ConfiguredToken)), new FakeGatewayEnvironment());

        Assert.Equal(ConfiguredToken, settings.Token);
    }

    /// <summary>With no password anywhere, there is no password and nothing verifies.</summary>
    [Fact]
    public void Password_NoneConfigured_HasNoPassword()
    {
        using TempDataDir dir = new();

        GatewaySettings settings = GatewaySettings.ReadForStartup(dir.Path, Config(), new FakeGatewayEnvironment());

        Assert.False(settings.HasPassword);
        Assert.False(settings.VerifyPassword("anything"));
        Assert.False(settings.VerifyPassword(string.Empty));
    }

    /// <summary>A password in configuration verifies.</summary>
    [Fact]
    public void Password_FromConfiguration_Verifies()
    {
        using TempDataDir dir = new();

        GatewaySettings settings = GatewaySettings.ReadForStartup(dir.Path, Config(("Team:Gateway:Auth:Password", "config-password")), new FakeGatewayEnvironment());

        Assert.True(settings.HasPassword);
        Assert.True(settings.VerifyPassword("config-password"));
        Assert.False(settings.VerifyPassword("other"));
    }

    /// <summary><c>HUDDLE_GATEWAY_PASSWORD</c> supplies the password when configuration does not.</summary>
    [Fact]
    public void Password_FromEnvironment_Verifies()
    {
        using TempDataDir dir = new();
        FakeGatewayEnvironment environment = new FakeGatewayEnvironment().With("HUDDLE_GATEWAY_PASSWORD", "env-password");

        GatewaySettings settings = GatewaySettings.ReadForStartup(dir.Path, Config(), environment);

        Assert.True(settings.HasPassword);
        Assert.True(settings.VerifyPassword("env-password"));
    }

    /// <summary>A password set in Settings (stored as a hash) verifies after a restart.</summary>
    [Fact]
    public void Password_Stored_Verifies()
    {
        using TempDataDir dir = new();
        new GatewaySettingsStore(dir.Path).SetPassword("stored-password");

        GatewaySettings settings = GatewaySettings.ReadForStartup(dir.Path, Config(), new FakeGatewayEnvironment());

        Assert.True(settings.HasPassword);
        Assert.True(settings.VerifyPassword("stored-password"));
        Assert.False(settings.VerifyPassword("stored-passwor"));
    }

    /// <summary>The password resolves like the token: configuration, then environment, then stored; a higher source hides the lower ones.</summary>
    [Fact]
    public void Password_ResolutionOrder_ConfigurationThenEnvironmentThenStored()
    {
        using TempDataDir dir = new();
        new GatewaySettingsStore(dir.Path).SetPassword("stored-password");
        FakeGatewayEnvironment environment = new FakeGatewayEnvironment().With("HUDDLE_GATEWAY_PASSWORD", "env-password");

        GatewaySettings withEnvironment = GatewaySettings.ReadForStartup(dir.Path, Config(), environment);
        GatewaySettings withConfiguration = GatewaySettings.ReadForStartup(dir.Path, Config(("Team:Gateway:Auth:Password", "config-password")), environment);

        Assert.True(withEnvironment.VerifyPassword("env-password"));
        Assert.False(withEnvironment.VerifyPassword("stored-password"));
        Assert.True(withConfiguration.VerifyPassword("config-password"));
        Assert.False(withConfiguration.VerifyPassword("env-password"));
        Assert.False(withConfiguration.VerifyPassword("stored-password"));
    }

    /// <summary>The Appendix A defaults hold when nothing is configured.</summary>
    [Fact]
    public void Options_Defaults_MatchAppendixA()
    {
        using TempDataDir dir = new();

        GatewayOptions options = GatewaySettings.ReadForStartup(dir.Path, Config(), new FakeGatewayEnvironment()).Options;

        Assert.Equal("loopback", options.Bind);
        Assert.Equal(GatewayAuthMode.Token, options.Auth.Mode);
        Assert.True(options.AutoApproveLocal);
        Assert.Null(options.Port);
        Assert.Equal(10, options.Auth.RateLimit.MaxAttempts);
        Assert.Equal(60, options.Auth.RateLimit.WindowSeconds);
        Assert.Equal(300, options.Auth.RateLimit.LockoutSeconds);
        Assert.True(options.Auth.RateLimit.ExemptLoopback);
    }

    /// <summary>The auth mode binds from its configuration text, including the hyphenated <c>trusted-proxy</c>.</summary>
    [Theory]
    [InlineData("token", "Token")]
    [InlineData("password", "Password")]
    [InlineData("trusted-proxy", "TrustedProxy")]
    [InlineData("none", "None")]
    [InlineData("Password", "Password")]
    public void Options_AuthMode_BindsFromText(string text, string expectedMember)
    {
        using TempDataDir dir = new();

        GatewayOptions options = GatewaySettings.ReadForStartup(dir.Path, Config(("Team:Gateway:Auth:Mode", text)), new FakeGatewayEnvironment()).Options;

        Assert.Equal(expectedMember, options.Auth.Mode.ToString());
    }

    /// <summary>Other Appendix A keys bind from <c>Team:Gateway</c>.</summary>
    [Fact]
    public void Options_OtherKeys_BindFromConfiguration()
    {
        using TempDataDir dir = new();
        IConfigurationRoot configuration = Config(
            ("Team:Gateway:Bind", "lan"),
            ("Team:Gateway:Port", "5101"),
            ("Team:Gateway:AutoApproveLocal", "false"),
            ("Team:Gateway:Auth:RateLimit:MaxAttempts", "3"),
            ("Team:Gateway:Auth:RateLimit:ExemptLoopback", "false"));

        GatewayOptions options = GatewaySettings.ReadForStartup(dir.Path, configuration, new FakeGatewayEnvironment()).Options;

        Assert.Equal("lan", options.Bind);
        Assert.Equal(5101, options.Port);
        Assert.False(options.AutoApproveLocal);
        Assert.Equal(3, options.Auth.RateLimit.MaxAttempts);
        Assert.False(options.Auth.RateLimit.ExemptLoopback);
    }
}
