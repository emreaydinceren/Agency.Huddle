using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>An <see cref="IGatewayRuntimeView"/> whose answers a test sets.</summary>
internal sealed class FakeGatewayRuntimeView : IGatewayRuntimeView
{
    /// <inheritdoc />
    public TailscaleState TailscaleState { get; set; } = TailscaleState.Missing;

    /// <inheritdoc />
    public string? TailscaleOwnerLogin { get; set; }

    /// <inheritdoc />
    public string? PublicUrl { get; set; } = "https://desktop-a.tail1234.ts.net:5101";

    /// <inheritdoc />
    public string? ListenerReason { get; set; }

    /// <inheritdoc />
    public IReadOnlyList<TailscaleServeStatus.Route> StrayRoutes { get; set; } = [];

    /// <inheritdoc />
    public bool BareNameHostInUse { get; set; }
}

/// <summary>An <see cref="IQrPngEncoder"/> that records what it was asked to encode and returns fixed bytes.</summary>
internal sealed class RecordingQrEncoder : IQrPngEncoder
{
    /// <summary>The bytes every call returns; <c>base64</c> of them is <c>AQID</c>.</summary>
    public static readonly byte[] Png = [1, 2, 3];

    /// <summary>Every payload encoded, in order.</summary>
    public List<string> Payloads { get; } = [];

    /// <inheritdoc />
    public byte[] EncodePng(string payload)
    {
        this.Payloads.Add(payload);
        return Png;
    }
}
