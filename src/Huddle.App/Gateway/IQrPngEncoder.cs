namespace Agency.Huddle.App.Gateway;

/// <summary>Turns text into a QR code PNG (Spec 6.9); a seam so a test can see what the setup link encoded.</summary>
internal interface IQrPngEncoder
{
    /// <summary>Encodes <paramref name="payload"/> as a PNG image.</summary>
    /// <param name="payload">The text to encode, for example a setup link.</param>
    /// <returns>The PNG bytes.</returns>
    byte[] EncodePng(string payload);
}
