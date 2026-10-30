namespace Agency.Huddle.App.Gateway;

/// <summary>The resolved gateway settings and secrets (Spec 6.1).</summary>
internal sealed class GatewaySettings
{
    /// <summary>The environment variable that supplies the gateway token.</summary>
    public const string TokenEnvironmentVariable = "HUDDLE_GATEWAY_TOKEN";

    /// <summary>The environment variable that supplies the gateway password.</summary>
    public const string PasswordEnvironmentVariable = "HUDDLE_GATEWAY_PASSWORD";

    private static readonly string[] Placeholders = ["undefined", "null", "__OPENCLAW_REDACTED__", "__HUDDLE_REDACTED__"];

    private readonly string? clearPassword;
    private readonly byte[]? passwordHash;

    private GatewaySettings(
        GatewayOptions options,
        string instanceId,
        string token,
        GatewaySecretSource tokenSource,
        string? clearPassword,
        byte[]? passwordHash)
    {
        this.Options = options;
        this.InstanceId = instanceId;
        this.Token = token;
        this.TokenSource = tokenSource;
        this.clearPassword = clearPassword;
        this.passwordHash = passwordHash;
    }

    /// <summary>The options bound from <c>Team:Gateway</c>.</summary>
    public GatewayOptions Options { get; }

    /// <summary>The Instance id.</summary>
    public string InstanceId { get; }

    /// <summary>The effective gateway token.</summary>
    public string Token { get; }

    /// <summary>Where <see cref="Token"/> came from.</summary>
    public GatewaySecretSource TokenSource { get; }

    /// <summary>Whether any gateway password is configured, in the environment or stored.</summary>
    public bool HasPassword => this.clearPassword is not null || this.passwordHash is not null;

    /// <summary>Whether <paramref name="presented"/> is the gateway token.</summary>
    /// <param name="presented">The presented token.</param>
    public bool VerifyToken(string presented)
    {
        ArgumentNullException.ThrowIfNull(presented);

        return GatewaySecrets.SecretsEqual(presented, this.Token);
    }

    /// <summary>Whether <paramref name="presented"/> is the gateway password (configuration, else environment, else stored).</summary>
    /// <param name="presented">The presented password.</param>
    public bool VerifyPassword(string presented)
    {
        ArgumentNullException.ThrowIfNull(presented);

        if (this.clearPassword is not null)
        {
            return GatewaySecrets.SecretsEqual(presented, this.clearPassword);
        }

        return this.passwordHash is not null && GatewaySecrets.VerifyPassword(presented, this.passwordHash);
    }

    /// <summary>
    /// Reads the settings before the app is built, with no dependency injection: binds <c>Team:Gateway</c>, opens the
    /// store, and resolves the token configuration, then environment, then stored (generating one when none).
    /// </summary>
    /// <param name="dataDir">The data directory holding <c>team.db</c>.</param>
    /// <param name="configuration">The configuration root.</param>
    /// <param name="environment">The environment seam; <see langword="null"/> reads the process environment.</param>
    /// <param name="timeProvider">Defaults to <see cref="TimeProvider.System"/>.</param>
    /// <exception cref="InvalidOperationException">A supplied token or password is whitespace or a placeholder, or the auth mode is unknown.</exception>
    public static GatewaySettings ReadForStartup(
        string dataDir,
        IConfiguration configuration,
        IGatewayEnvironment? environment = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDir);
        ArgumentNullException.ThrowIfNull(configuration);

        IGatewayEnvironment env = environment ?? new ProcessGatewayEnvironment();
        GatewayOptions options = new();
        configuration.GetSection(GatewayOptions.SectionName).Bind(options);
        _ = options.Auth.Mode;

        GatewaySettingsStore store = new(dataDir, timeProvider);

        string? configToken = Supplied(options.Auth.Token, "Team:Gateway:Auth:Token");
        string? envToken = Supplied(env.Get(TokenEnvironmentVariable), TokenEnvironmentVariable);
        (string token, GatewaySecretSource source) = (configToken, envToken) switch
        {
            ({ } fromConfig, _) => (fromConfig, GatewaySecretSource.Configuration),
            (_, { } fromEnvironment) => (fromEnvironment, GatewaySecretSource.Environment),
            _ => (store.EnsureToken(), GatewaySecretSource.Stored),
        };

        string? clearPassword =
            Supplied(options.Auth.Password, "Team:Gateway:Auth:Password")
            ?? Supplied(env.Get(PasswordEnvironmentVariable), PasswordEnvironmentVariable);
        byte[]? hash = clearPassword is null ? store.StoredPasswordHash : null;

        return new GatewaySettings(options, store.InstanceId, token, source, clearPassword, hash);
    }

    /// <summary>Null or empty means not supplied; whitespace or a placeholder literal is refused.</summary>
    private static string? Supplied(string? value, string source)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(value) || Placeholders.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{source} is blank or a placeholder; supply a real value or remove it.");
        }

        return value;
    }
}
