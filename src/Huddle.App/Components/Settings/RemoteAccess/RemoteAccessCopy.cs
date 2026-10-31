using System.Globalization;
using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.App.Components.Settings.RemoteAccess;

/// <summary>Every sentence the Remote access panel shows (Spec 6.12, wireframe G10), in one place so a test pins each one.</summary>
internal static class RemoteAccessCopy
{
    /// <summary>The intro under the section menu.</summary>
    public const string Intro = "Reach Huddle from other devices. Every device needs the gateway secret and your approval.";

    /// <summary>The intro while Strict local is on.</summary>
    public const string IntroStrictLocal = "Only this computer can use Huddle right now. Turn off Strict local to reach it from a phone, tablet or another computer.";

    /// <summary>What the token shows until Show is pressed.</summary>
    public const string TokenMask = "••••••••";

    /// <summary>The notice after Rotate.</summary>
    public const string TokenRotated = "Token rotated. Devices stay signed in.";

    /// <summary>The notice after Set password.</summary>
    public const string PasswordSet = "Password set.";

    /// <summary>The notice after Copy link.</summary>
    public const string LinkCopied = "Link copied.";

    /// <summary>The sentence under the QR code.</summary>
    public const string PairNote = "Scan to pair. Works once, for 10 minutes. You will still approve it.";

    /// <summary>Why there is no QR code when the offer gave no reason.</summary>
    public const string NoAddress = "There is no address to pair against yet. Turn on Tailscale, or set a LAN bind and a public origin.";

    /// <summary>The next step when Tailscale is chosen and signed out.</summary>
    public const string TailscaleSignedOut = "Tailscale is signed out. Sign in to Tailscale, then restart Huddle.";

    /// <summary>The next step when Tailscale is chosen and not installed.</summary>
    public const string TailscaleMissing = "Tailscale is not installed. Install it from tailscale.com, then restart Huddle.";

    /// <summary>The next step when Tailscale is chosen and not running.</summary>
    public const string TailscaleNotRunning = "Tailscale is not running. Start Tailscale, then restart Huddle.";

    /// <summary>The helper text under the Strict local switch.</summary>
    public const string StrictLocalHelper = "Only this computer can use Huddle, with no sign-in. Turn off to let other devices connect.";

    /// <summary>The one line shown while strict local is on and the other Gateway controls are disabled.</summary>
    public const string StrictLocalHint = "Turn off Strict local to set up remote access.";

    /// <summary>Shown when strict local was switched on while a network listener or Tailscale route is still running.</summary>
    public const string CloseListenersLine = "Restart Huddle to close the network listeners.";

    /// <summary>The warning on the Gateway card while plain HTTP is offered to other devices (Spec Appendix B).</summary>
    public const string DowngradedNotice = "Plain HTTP: anyone on this network can read what Huddle sends.";

    /// <summary>The style that lets a long link wrap inside the card.</summary>
    public const string WrapStyle = "overflow-wrap: anywhere;";

    /// <summary>The line under the Gateway card when a saved setting waits for a restart.</summary>
    /// <param name="pending">The settings that differ, in order.</param>
    public static string RestartLine(IReadOnlyList<GatewayAdminService.PendingChange> pending)
    {
        ArgumentNullException.ThrowIfNull(pending);

        return "Restart Huddle to apply: " + string.Join(", ", pending.Select(static change => $"{change.Setting} {change.Running} → {change.Stored}"));
    }

    /// <summary>What Save reports: what applies now and what waits for the next start.</summary>
    /// <param name="result">The save result.</param>
    public static string SaveResult(GatewayAdminService.SaveResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        string text = "Saved.";
        if (result.AppliedNow.Count > 0)
        {
            text += " Applies now: " + string.Join(", ", result.AppliedNow);
        }

        if (result.NextStart.Count > 0)
        {
            text += " Applies at the next start: " + string.Join(", ", result.NextStart);
        }

        return text;
    }

    /// <summary>The ingress of a request as a word; only a direct connection is "downgraded".</summary>
    /// <param name="ingress">The ingress kind as text.</param>
    /// <param name="bind">The running bind.</param>
    /// <param name="tailscaleMode">The running Tailscale mode.</param>
    public static string IngressLabel(string ingress, string bind = "", string tailscaleMode = "")
    {
        ArgumentNullException.ThrowIfNull(ingress);

        return ingress switch
        {
            "Loopback" => "This computer",
            "Direct" => DirectLabel(bind),
            "Managed" => string.Equals(tailscaleMode, "funnel", StringComparison.OrdinalIgnoreCase) ? "Tailscale Funnel" : "Tailscale",
            "TrustedProxy" => "Proxy",
            _ => ingress,
        };
    }

    /// <summary>Whether a running bind sends plain HTTP to other devices, which the Gateway card calls downgraded.</summary>
    /// <param name="bind">The running bind.</param>
    public static bool IsDowngradedBind(string bind) =>
        string.Equals(bind, "lan", StringComparison.OrdinalIgnoreCase)
        || string.Equals(bind, "tailnet", StringComparison.OrdinalIgnoreCase)
        || string.Equals(bind, "custom", StringComparison.OrdinalIgnoreCase);

    private static string DirectLabel(string bind)
    {
        if (string.Equals(bind, "lan", StringComparison.OrdinalIgnoreCase))
        {
            return "LAN (downgraded)";
        }

        return string.Equals(bind, "tailnet", StringComparison.OrdinalIgnoreCase) ? "Tailscale address (downgraded)" : "Network (downgraded)";
    }

    /// <summary>Time left as <c>m:ss</c>, never negative.</summary>
    /// <param name="left">The time left.</param>
    public static string Countdown(TimeSpan left)
    {
        int seconds = (int)Math.Max(0, Math.Floor(left.TotalSeconds));
        return string.Create(CultureInfo.InvariantCulture, $"{seconds / 60}:{seconds % 60:00}");
    }

    /// <summary>When a device was last seen: now, or minutes, hours or days ago.</summary>
    /// <param name="ago">How long ago.</param>
    public static string LastSeen(TimeSpan ago)
    {
        if (ago < TimeSpan.FromMinutes(1))
        {
            return "seen now";
        }

        if (ago < TimeSpan.FromHours(1))
        {
            return string.Create(CultureInfo.InvariantCulture, $"seen {(int)ago.TotalMinutes} min");
        }

        return ago < TimeSpan.FromDays(1)
            ? string.Create(CultureInfo.InvariantCulture, $"seen {(int)ago.TotalHours} h")
            : string.Create(CultureInfo.InvariantCulture, $"seen {(int)ago.TotalDays} d");
    }

    /// <summary>The question in the row before a device is removed.</summary>
    /// <param name="name">The device name.</param>
    public static string RemovePrompt(string name) => $"Remove {name}? It will have to pair again.";
}
