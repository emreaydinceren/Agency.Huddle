using QRCoder;

namespace Agency.Huddle.App.Gateway;

/// <summary>The QRCoder (MIT) implementation of <see cref="IQrPngEncoder"/>: error correction level M, a PNG with a quiet zone.</summary>
internal sealed class QrCoderPngEncoder : IQrPngEncoder
{
    private const int PixelsPerModule = 8;

    /// <inheritdoc />
    public byte[] EncodePng(string payload)
    {
        ArgumentException.ThrowIfNullOrEmpty(payload);

        using QRCodeGenerator generator = new();
        using QRCodeData data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        PngByteQRCode code = new(data);
        return code.GetGraphic(PixelsPerModule);
    }
}
