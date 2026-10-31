using System.Net;
using Agency.Huddle.App.Desktop;
using Agency.Huddle.Tests.Gateway;

namespace Agency.Huddle.Tests.Desktop;

/// <summary>Spec 6.9, C-9 and C-12: a second launch asks the running copy for a bootstrap URL and falls back to <c>{url}/auth</c> when it cannot.</summary>
public sealed class DesktopHandOffBootstrapTests
{
    private const string BootstrapUrl = "http://127.0.0.1:5123/auth?return=%2F#bootstrapToken=abcDEF_-0123456789abcdefghijklmnopqrstuvw";

    private static readonly InstanceInfo Info = new("http://127.0.0.1:5123", "stop-token-1", 4242);

    private static async Task<(HandOffResult Result, RecordingBrowserOpener Opener)> RunAsync(ScriptedHttpHandler handler, CancellationToken ct)
    {
        using TempDataDir dir = new();
        InstanceFile.Write(dir.Path, Info);
        using InstanceLock? owner = InstanceLock.TryAcquire(dir.Path);
        Assert.NotNull(owner);
        RecordingBrowserOpener opener = new();

        HandOffResult result = await DesktopHandOff.AcquireOrHandOffAsync(
            dir.Path, opener, handler, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(20), ct);
        return (result, opener);
    }

    /// <summary>The running copy's answer is opened; the call was a POST to <c>/_desktop/bootstrap</c> carrying the instance token.</summary>
    [Fact]
    public async Task HandOff_RunningCopyAnswers_OpensTheBootstrapUrl()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ScriptedHttpHandler handler = ScriptedHttpHandler.Json(HttpStatusCode.OK, $$"""{"url":"{{BootstrapUrl}}"}""");

        (HandOffResult result, RecordingBrowserOpener opener) = await RunAsync(handler, ct);

        Assert.Null(result.Lock);
        Assert.True(result.Opened);
        Assert.Equal([BootstrapUrl], opener.Opened);
        ScriptedRequest request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://127.0.0.1:5123/_desktop/bootstrap", request.Uri.AbsoluteUri);
        Assert.Equal("stop-token-1", request.InstanceToken);
    }

    /// <summary>When the call is refused or unsupported (an older copy, 401, 404, 405) the browser opens <c>{url}/auth</c>.</summary>
    /// <param name="status">The status the running copy answers.</param>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.MethodNotAllowed)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task HandOff_CallFails_OpensTheAuthPage(HttpStatusCode status)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ScriptedHttpHandler handler = ScriptedHttpHandler.Json(status, "{}");

        (HandOffResult result, RecordingBrowserOpener opener) = await RunAsync(handler, ct);

        Assert.True(result.Opened);
        Assert.Equal(["http://127.0.0.1:5123/auth"], opener.Opened);
    }

    /// <summary>A connection error, or a 200 whose body has no url, also falls back to <c>{url}/auth</c>.</summary>
    /// <param name="throws">Whether the handler throws rather than answering 200 with an empty object.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandOff_NoUsableAnswer_OpensTheAuthPage(bool throws)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ScriptedHttpHandler handler = throws
            ? ScriptedHttpHandler.Throwing(new HttpRequestException("refused"))
            : ScriptedHttpHandler.Json(HttpStatusCode.OK, "{}");

        (HandOffResult result, RecordingBrowserOpener opener) = await RunAsync(handler, ct);

        Assert.True(result.Opened);
        Assert.Equal(["http://127.0.0.1:5123/auth"], opener.Opened);
    }
}
