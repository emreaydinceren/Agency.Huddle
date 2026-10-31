using Agency.Huddle.MockAdapter;

namespace Agency.Huddle.Tests.MockAdapter;

/// <summary>
/// Pins the adapter that lets <c>FakeAcpAgent</c>, which takes one <see cref="Stream"/>, run over
/// a process's separate stdin and stdout. See Spec §6.10 ("Two modes").
/// </summary>
public sealed class DuplexStreamTests
{
    /// <summary>Bytes written to the duplex arrive on the supplied output stream.</summary>
    [Fact]
    public async Task WriteAsync_BytesWritten_ArriveOnOutputStream()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MemoryStream input = new();
        MemoryStream output = new();
        DuplexStream duplex = new(input, output);
        byte[] payload = "hello"u8.ToArray();

        await duplex.WriteAsync(payload, ct);
        await duplex.FlushAsync(ct);

        Assert.Equal(payload, output.ToArray());
    }

    /// <summary>Bytes placed on the supplied input stream are read back from the duplex.</summary>
    [Fact]
    public async Task ReadAsync_BytesOnInputStream_AreReadBack()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        byte[] payload = "hello"u8.ToArray();
        MemoryStream input = new(payload);
        MemoryStream output = new();
        DuplexStream duplex = new(input, output);
        byte[] buffer = new byte[payload.Length];

        int bytesRead = await duplex.ReadAsync(buffer, ct);

        Assert.Equal(payload.Length, bytesRead);
        Assert.Equal(payload, buffer);
    }

    /// <summary>The duplex reports readable and writable, but not seekable.</summary>
    [Fact]
    public void Capabilities_CanReadAndCanWriteAreTrue_CanSeekIsFalse()
    {
        DuplexStream duplex = new(new MemoryStream(), new MemoryStream());

        Assert.True(duplex.CanRead);
        Assert.True(duplex.CanWrite);
        Assert.False(duplex.CanSeek);
    }

    /// <summary>Disposing a duplex constructed with leaveOpen: true disposes neither half.</summary>
    [Fact]
    public void Dispose_LeaveOpenTrue_DisposesNeitherHalf()
    {
        MemoryStream input = new();
        MemoryStream output = new();
        DuplexStream duplex = new(input, output, leaveOpen: true);

        duplex.Dispose();

        Assert.True(input.CanRead);
        Assert.True(output.CanWrite);
    }

    /// <summary>Disposing a duplex constructed with the default leaveOpen: false disposes both halves.</summary>
    [Fact]
    public void Dispose_LeaveOpenFalse_DisposesBothHalves()
    {
        MemoryStream input = new();
        MemoryStream output = new();
        DuplexStream duplex = new(input, output);

        duplex.Dispose();

        Assert.False(input.CanRead);
        Assert.False(output.CanWrite);
    }

    /// <summary>
    /// Per the <see cref="Stream"/> contract, a disposed duplex reports CanRead and CanWrite as
    /// false rather than throwing.
    /// </summary>
    [Fact]
    public void Capabilities_AfterDispose_ReportFalseWithoutThrowing()
    {
        DuplexStream duplex = new(new MemoryStream(), new MemoryStream());

        duplex.Dispose();

        Assert.False(duplex.CanRead);
        Assert.False(duplex.CanWrite);
    }
}
