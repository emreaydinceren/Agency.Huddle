namespace Agency.Huddle.App.Gateway;

/// <summary>The <c>auth</c> object of <c>POST /auth/connect</c>: at most one credential is normally present.</summary>
/// <param name="Token">The gateway token.</param>
/// <param name="Password">The gateway password.</param>
/// <param name="DeviceToken">This device's token.</param>
/// <param name="BootstrapToken">A bootstrap token.</param>
internal sealed record ConnectAuth(
    string? Token = null,
    string? Password = null,
    string? DeviceToken = null,
    string? BootstrapToken = null)
{
    /// <summary>The credential the signature covers, in OpenClaw's order: device token, else token or password, else bootstrap token, else empty (Spec 6.5).</summary>
    public string SignedCredential => this.DeviceToken ?? this.Token ?? this.Password ?? this.BootstrapToken ?? string.Empty;
}
