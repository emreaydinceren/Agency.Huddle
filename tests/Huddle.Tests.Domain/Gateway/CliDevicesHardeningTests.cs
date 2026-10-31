using System.Net;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Review fix R-8: the <c>devices</c> verbs print no terminal control characters and talk only to a loopback http URL.</summary>
public sealed class CliDevicesHardeningTests
{
    /// <summary>Control characters, ESC included, in a device name and in the printed address columns are stripped before printing.</summary>
    [Fact]
    public async Task List_ControlCharactersInFields_AreStripped()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CliHarness harness = new();
        using ScriptedHttpHandler handler = ScriptedHttpHandler.Json(
            HttpStatusCode.OK,
            """
            {"devices":[{"deviceId":"dev-1","name":"Pix\u001b[2Jel\u009b\u0007","scopes":["operator.read"],"pairedVia":"appr\u001boval"}],
             "requests":[{"requestId":"req-9","name":"Lap\u001btop","scopes":[],"address":"203.0.113.9\u001b]0;x","userAgent":"UA\u0007","ingress":"La\u001bn"}]}
            """);

        int code = await harness.RunAsync(handler, ct, "devices", "list", "--url", "http://127.0.0.1:5200");

        Assert.Equal(0, code);
        string output = harness.Console.OutText;
        Assert.Equal(
            string.Join(Environment.NewLine, "Devices:", "  dev-1  Pix[2Jel  [operator.read]  approval", "Pending requests:", "  req-9  Laptop  []  Lan", string.Empty),
            output);
    }

    /// <summary>A <c>--url</c> that is not a loopback http URL exits 2 with a message and sends nothing.</summary>
    [Theory]
    [InlineData("http://example.com:5200")]
    [InlineData("https://127.0.0.1:5200")]
    [InlineData("http://192.168.1.5:5200")]
    [InlineData("ftp://localhost")]
    [InlineData("not a url")]
    public async Task List_NonLoopbackUrl_ExitsTwoAndSendsNothing(string url)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CliHarness harness = new();
        using ScriptedHttpHandler handler = ScriptedHttpHandler.Json(HttpStatusCode.OK, "{}");

        int code = await harness.RunAsync(handler, ct, "devices", "list", "--url", url);

        Assert.Equal(2, code);
        Assert.Empty(handler.Requests);
        Assert.Equal(
            "--url must be a loopback http URL (http://127.0.0.1:PORT, http://[::1]:PORT or http://localhost:PORT); nothing was sent." + Environment.NewLine,
            harness.Console.ErrorText);
    }

    /// <summary>The three loopback spellings are accepted.</summary>
    [Theory]
    [InlineData("http://127.0.0.1:5200")]
    [InlineData("http://[::1]:5200")]
    [InlineData("http://localhost:5200")]
    public async Task List_LoopbackUrl_IsAccepted(string url)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CliHarness harness = new();
        using ScriptedHttpHandler handler = ScriptedHttpHandler.Json(HttpStatusCode.OK, "{}");

        int code = await harness.RunAsync(handler, ct, "devices", "list", "--url", url);

        Assert.Equal(0, code);
        Assert.Single(handler.Requests);
    }
}
