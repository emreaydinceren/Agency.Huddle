using System.Net;
using System.Text.Json;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Spec 6.9, 6.13 and C-10: <c>dashboard</c> asks a running desktop copy for a bootstrap URL, opens it, or prints it with <c>--json</c>.</summary>
public sealed class CliDashboardTests
{
    private const string Url = "http://127.0.0.1:5123/auth?return=%2F#bootstrapToken=tok";

    private static ScriptedHttpHandler Answer(HttpStatusCode status = HttpStatusCode.OK)
        => ScriptedHttpHandler.Json(status, $$"""{"url":"{{Url}}"}""");

    /// <summary>With no instance record, it prints "No desktop copy is running." and exits 1 without a call or a browser.</summary>
    [Fact]
    public async Task Dashboard_NoCopy_SaysSoAndExits1()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CliHarness harness = new();
        using ScriptedHttpHandler handler = Answer();

        int code = await harness.RunAsync(handler, ct, "dashboard");

        Assert.Equal(1, code);
        Assert.Equal("No desktop copy is running." + Environment.NewLine, harness.Console.ErrorText);
        Assert.Empty(handler.Requests);
        Assert.Empty(harness.Opener.Opened);
    }

    /// <summary>A record whose owner is gone (the lock is free) counts as no copy running.</summary>
    [Fact]
    public async Task Dashboard_StaleRecord_SaysNoCopyAndExits1()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CliHarness harness = new();
        harness.WriteStaleRecord();
        using ScriptedHttpHandler handler = Answer();

        int code = await harness.RunAsync(handler, ct, "dashboard");

        Assert.Equal(1, code);
        Assert.Equal("No desktop copy is running." + Environment.NewLine, harness.Console.ErrorText);
        Assert.Empty(handler.Requests);
    }

    /// <summary>A running copy: POST to its <c>/_desktop/bootstrap</c> with the instance token, open the URL it returns, exit 0, and do not print the token URL.</summary>
    [Fact]
    public async Task Dashboard_RunningCopy_OpensTheReturnedUrl()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CliHarness harness = new();
        harness.StartRunningCopy();
        using ScriptedHttpHandler handler = Answer();

        int code = await harness.RunAsync(handler, ct, "dashboard");

        Assert.Equal(0, code);
        Assert.Equal([Url], harness.Opener.Opened);
        ScriptedRequest request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://127.0.0.1:5123/_desktop/bootstrap", request.Uri.AbsoluteUri);
        Assert.Equal(CliHarness.InstanceToken, request.InstanceToken);
        Assert.DoesNotContain("bootstrapToken", harness.Console.AllText, StringComparison.Ordinal);
    }

    /// <summary><c>--json</c> prints <c>{ "url": ... }</c> to standard output and opens nothing.</summary>
    [Fact]
    public async Task Dashboard_Json_PrintsTheUrlAndOpensNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CliHarness harness = new();
        harness.StartRunningCopy();
        using ScriptedHttpHandler handler = Answer();

        int code = await harness.RunAsync(handler, ct, "dashboard", "--json");

        Assert.Equal(0, code);
        using JsonDocument json = JsonDocument.Parse(harness.Console.OutText);
        Assert.Equal(Url, json.RootElement.GetProperty("url").GetString());
        Assert.Empty(harness.Opener.Opened);
    }

    /// <summary>A refusal (401) or a connection error exits 1 with a message and opens nothing.</summary>
    /// <param name="throws">Whether the handler throws a connection error rather than answering 401.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dashboard_CopyRefusesOrIsUnreachable_Exits1(bool throws)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CliHarness harness = new();
        harness.StartRunningCopy();
        using ScriptedHttpHandler handler = throws
            ? ScriptedHttpHandler.Throwing(new HttpRequestException("refused"))
            : Answer(HttpStatusCode.Unauthorized);

        int code = await harness.RunAsync(handler, ct, "dashboard");

        Assert.Equal(1, code);
        Assert.False(string.IsNullOrWhiteSpace(harness.Console.AllText));
        Assert.Empty(harness.Opener.Opened);
    }
}
