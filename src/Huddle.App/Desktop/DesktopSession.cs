using System.Security.Cryptography;

namespace Agency.Huddle.App.Desktop;

/// <summary>
/// The singleton session for the running desktop instance, holding the stop token needed to
/// authenticate the stop endpoint.
/// </summary>
public sealed class DesktopSession
{
    /// <summary>
    /// A 43-character base64url-encoded cryptographically random token, generated once per
    /// instance and used to authenticate <c>POST /_desktop/stop</c> requests.
    /// </summary>
    public string StopToken { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DesktopSession"/> class with a randomly
    /// generated stop token.
    /// </summary>
    public DesktopSession()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        string base64 = Convert.ToBase64String(bytes);
        this.StopToken = base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
