using System.Text.Json;

namespace Agency.Huddle.App.Gateway;

/// <summary>
/// The settings Settings can change (Spec 6.12, 9), stored as a JSON object keyed by option path under <c>Team:Gateway</c> in
/// <c>gateway_settings.overrides</c>. <see cref="GatewaySettings.ReadForStartup"/> applies them over configuration.
/// </summary>
internal static class GatewayOverrides
{
    /// <summary>The key of <see cref="GatewayOptions.StrictLocal"/>.</summary>
    public const string StrictLocal = "StrictLocal";

    /// <summary>The key of <see cref="GatewayOptions.Bind"/>.</summary>
    public const string Bind = "Bind";

    /// <summary>The key of the auth mode.</summary>
    public const string AuthMode = "Auth:Mode";

    /// <summary>The key of <see cref="GatewayOptions.Port"/>.</summary>
    public const string Port = "Port";

    /// <summary>The key of <see cref="GatewayOptions.CustomBindHost"/>.</summary>
    public const string CustomBindHost = "CustomBindHost";

    /// <summary>The key of the Tailscale mode.</summary>
    public const string TailscaleMode = "Tailscale:Mode";

    /// <summary>The key of the Tailscale HTTPS port.</summary>
    public const string TailscaleHttpsPort = "Tailscale:HttpsPort";

    /// <summary>The key of <see cref="GatewayAuthOptions.AllowTailscale"/>.</summary>
    public const string AllowTailscale = "Auth:AllowTailscale";

    /// <summary>The key of <see cref="GatewayOptions.TrustedProxies"/>.</summary>
    public const string TrustedProxies = "TrustedProxies";

    /// <summary>The key of <see cref="GatewayOptions.AllowedOrigins"/>.</summary>
    public const string AllowedOrigins = "AllowedOrigins";

    /// <summary>The key of <see cref="GatewayOptions.PublicOrigin"/>.</summary>
    public const string PublicOrigin = "PublicOrigin";

    /// <summary>Sets every option <paramref name="json"/> names. Text that is not a JSON object changes nothing; a value of the wrong type is skipped.</summary>
    /// <param name="options">The options to change.</param>
    /// <param name="json">The stored overrides.</param>
    public static void Apply(GatewayOptions options, string json)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                ApplyOne(options, property.Name, property.Value);
            }
        }
        catch (JsonException)
        {
            // A corrupt overrides cell must not stop Huddle from starting: the configured values apply and the next save rewrites it.
        }
    }

    /// <summary>A copy of the settings Settings can change, so the stored view can be computed without touching the running options.</summary>
    /// <param name="source">The options to copy.</param>
    public static GatewayOptions Copy(GatewayOptions source)
    {
        ArgumentNullException.ThrowIfNull(source);

        GatewayOptions copy = new()
        {
            StrictLocal = source.StrictLocal,
            Bind = source.Bind,
            Port = source.Port,
            CustomBindHost = source.CustomBindHost,
            PublicOrigin = source.PublicOrigin,
        };
        copy.Auth.ModeText = source.Auth.ModeText;
        copy.Auth.AllowTailscale = source.Auth.AllowTailscale;
        copy.Tailscale.Mode = source.Tailscale.Mode;
        copy.Tailscale.HttpsPort = source.Tailscale.HttpsPort;
        copy.TrustedProxies.AddRange(source.TrustedProxies);
        copy.AllowedOrigins.AddRange(source.AllowedOrigins);
        return copy;
    }

    private static void ApplyOne(GatewayOptions options, string key, JsonElement value)
    {
        switch (key)
        {
            case StrictLocal when value.ValueKind is JsonValueKind.True or JsonValueKind.False:
                options.StrictLocal = value.GetBoolean();
                break;
            case Bind when Text(value) is { } bind:
                options.Bind = bind;
                break;
            case AuthMode when Text(value) is { } mode:
                options.Auth.ModeText = mode;
                break;
            case Port when Number(value, out int? port):
                options.Port = port;
                break;
            case CustomBindHost when value.ValueKind is JsonValueKind.String or JsonValueKind.Null:
                options.CustomBindHost = value.GetString();
                break;
            case TailscaleMode when Text(value) is { } tailscale:
                options.Tailscale.Mode = tailscale;
                break;
            case TailscaleHttpsPort when Number(value, out int? httpsPort):
                options.Tailscale.HttpsPort = httpsPort;
                break;
            case AllowTailscale when value.ValueKind is JsonValueKind.True or JsonValueKind.False:
                options.Auth.AllowTailscale = value.GetBoolean();
                break;
            case TrustedProxies when value.ValueKind == JsonValueKind.Array:
                Replace(options.TrustedProxies, value);
                break;
            case AllowedOrigins when value.ValueKind == JsonValueKind.Array:
                Replace(options.AllowedOrigins, value);
                break;
            case PublicOrigin when value.ValueKind is JsonValueKind.String or JsonValueKind.Null:
                options.PublicOrigin = value.GetString();
                break;
            default:
                break;
        }
    }

    private static string? Text(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool Number(JsonElement value, out int? number)
    {
        number = null;
        if (value.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int parsed))
        {
            number = parsed;
            return true;
        }

        return false;
    }

    private static void Replace(List<string> target, JsonElement array)
    {
        List<string> next = [.. array.EnumerateArray().Where(static item => item.ValueKind == JsonValueKind.String).Select(static item => item.GetString() ?? string.Empty)];
        target.Clear();
        target.AddRange(next);
    }
}
