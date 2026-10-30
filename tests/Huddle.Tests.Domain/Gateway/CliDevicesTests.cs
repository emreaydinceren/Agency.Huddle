using System.Net;
using System.Text.Json;
using Agency.Huddle.App.Cli;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Spec 6.13 and C-10: the <c>devices</c> verbs talk to <c>/_gateway/*</c> with the gateway token as a bearer, at <c>instance.json</c>'s URL or <c>--url</c>.</summary>
public sealed class CliDevicesTests
{
    private const string ListJson = """
        {"devices":[{"deviceId":"dev-1","name":"Pixel Tablet","scopes":["operator.read","operator.write"],"pairedVia":"approval"}],
         "requests":[{"requestId":"req-9","deviceId":"dev-2","name":"Work Laptop","scopes":["operator.read"],"address":"203.0.113.9","ingress":"Lan"}]}
        """;

    private static readonly string ListText = string.Join(
        Environment.NewLine,
        "Devices:",
        "  dev-1  Pixel Tablet  [operator.read,operator.write]  approval",
        "Pending requests:",
        "  req-9  Work Laptop  [operator.read]  Lan",
        string.Empty);

    private static ScriptedHttpHandler Ok(string json = "{}") => ScriptedHttpHandler.Json(HttpStatusCode.OK, json);

    /// <summary><c>list</c> with <c>--url</c> and no running copy: GET <c>/_gateway/devices</c> with the bearer, print devices and pending requests, exit 0.</summary>
    [Fact]
    public async Task List_WithUrlOverride_PrintsDevicesAndRequests()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CliHarness harness = new();
        using ScriptedHttpHandler handler = Ok(ListJson);

        int code = await harness.RunAsync(handler, ct, "devices", "list", "--url", "http://127.0.0.1:5200");

        Assert.Equal(0, code);
        ScriptedRequest request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("http://127.0.0.1:5200/_gateway/devices", request.Uri.AbsoluteUri);
        Assert.Equal("Bearer " + CliHarness.Token, request.Authorization);
        Assert.Equal(ListText, harness.Console.OutText);
    }

    /// <summary>Without <c>--url</c> the running copy's address comes from <c>instance.json</c>.</summary>
    [Fact]
    public async Task List_NoUrl_UsesTheInstanceRecord()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CliHarness harness = new();
        harness.StartRunningCopy("http://127.0.0.1:5321");
        using ScriptedHttpHandler handler = Ok(ListJson);

        int code = await harness.RunAsync(handler, ct, "devices", "list");

        Assert.Equal(0, code);
        Assert.Equal("http://127.0.0.1:5321/_gateway/devices", Assert.Single(handler.Requests).Uri.AbsoluteUri);
    }

    /// <summary>With neither a running copy nor <c>--url</c> there is nothing to talk to: a message that mentions <c>--url</c>, exit 1, no call.</summary>
    [Fact]
    public async Task List_NoCopyNoUrl_ExplainsAndExits1()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CliHarness harness = new();
        using ScriptedHttpHandler handler = Ok(ListJson);

        int code = await harness.RunAsync(handler, ct, "devices", "list");

        Assert.Equal(1, code);
        Assert.Equal("No desktop copy is running. Pass --url http://127.0.0.1:PORT to reach a copy started another way." + Environment.NewLine, harness.Console.ErrorText);
        Assert.Empty(handler.Requests);
    }

    /// <summary><c>approve &lt;id&gt;</c> posts to the request's approve route with no scope list and exits 0.</summary>
    [Fact]
    public async Task Approve_WithId_PostsWithoutScopes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CliHarness harness = new();
        harness.StartRunningCopy();
        using ScriptedHttpHandler handler = Ok();

        int code = await harness.RunAsync(handler, ct, "devices", "approve", "req-9");

        Assert.Equal(0, code);
        ScriptedRequest request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://127.0.0.1:5123/_gateway/devices/requests/req-9/approve", request.Uri.AbsoluteUri);
        Assert.Equal("Bearer " + CliHarness.Token, request.Authorization);
        Assert.DoesNotContain("scope", request.Body ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>Each <c>--scope</c> is sent in the body's <c>scope</c> list.</summary>
    [Fact]
    public async Task Approve_WithScopes_SendsTheScopeList()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CliHarness harness = new();
        harness.StartRunningCopy();
        using ScriptedHttpHandler handler = Ok();

        int code = await harness.RunAsync(handler, ct, "devices", "approve", "req-9", "--scope", "operator.read", "--scope", "operator.write");

        Assert.Equal(0, code);
        string? body = Assert.Single(handler.Requests).Body;
        Assert.NotNull(body);
        using JsonDocument json = JsonDocument.Parse(body);
        string?[] scopes = [.. json.RootElement.GetProperty("scope").EnumerateArray().Select(item => item.GetString())];
        Assert.Equal(["operator.read", "operator.write"], scopes);
    }

    /// <summary><c>approve</c> with no id only previews the pending requests (a GET), changes nothing and exits 1 (OpenClaw's behaviour).</summary>
    [Fact]
    public async Task Approve_NoId_PreviewsAndExits1()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CliHarness harness = new();
        harness.StartRunningCopy();
        using ScriptedHttpHandler handler = Ok(ListJson);

        int code = await harness.RunAsync(handler, ct, "devices", "approve");

        Assert.Equal(1, code);
        Assert.All(handler.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
        Assert.Equal(ListText, harness.Console.OutText);
    }

    /// <summary><c>reject &lt;id&gt;</c> posts to the request's reject route.</summary>
    [Fact]
    public async Task Reject_WithId_PostsToTheRejectRoute()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CliHarness harness = new();
        harness.StartRunningCopy();
        using ScriptedHttpHandler handler = Ok();

        int code = await harness.RunAsync(handler, ct, "devices", "reject", "req-9");

        Assert.Equal(0, code);
        ScriptedRequest request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://127.0.0.1:5123/_gateway/devices/requests/req-9/reject", request.Uri.AbsoluteUri);
    }

    /// <summary><c>revoke</c>, <c>remove</c> and <c>rotate</c> with <c>--device</c> post to the device's route.</summary>
    /// <param name="verb">The sub-verb.</param>
    [Theory]
    [InlineData("revoke")]
    [InlineData("remove")]
    [InlineData("rotate")]
    public async Task DeviceVerbs_PostToTheDeviceRoute(string verb)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CliHarness harness = new();
        harness.StartRunningCopy();
        using ScriptedHttpHandler handler = Ok();

        int code = await harness.RunAsync(handler, ct, "devices", verb, "--device", "dev-1");

        Assert.Equal(0, code);
        ScriptedRequest request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"http://127.0.0.1:5123/_gateway/devices/dev-1/{verb}", request.Uri.AbsoluteUri);
        Assert.Equal("Bearer " + CliHarness.Token, request.Authorization);
    }

    /// <summary>A device verb without <c>--device</c>, a missing sub-verb and an unknown one are usage errors (exit 2) and call nothing.</summary>
    /// <param name="args">The arguments after <c>devices</c>.</param>
    [Theory]
    [InlineData("revoke")]
    [InlineData("bogus")]
    [InlineData("reject")]
    public async Task Devices_BadUsage_Exits2(string args)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CliHarness harness = new();
        harness.StartRunningCopy();
        using ScriptedHttpHandler handler = Ok();

        int code = await harness.RunAsync(handler, ct, "devices", args);

        Assert.Equal(2, code);
        Assert.Empty(handler.Requests);
        Assert.Equal(CliVerbs.Usage + Environment.NewLine, harness.Console.ErrorText);
    }

    /// <summary>A 401 (wrong token), a 404 (unknown id) and a connection error each exit 1 with a message.</summary>
    /// <param name="status">The status, or 0 for a connection error.</param>
    [Theory]
    [InlineData(401)]
    [InlineData(404)]
    [InlineData(0)]
    public async Task Devices_FailureStatuses_Exit1(int status)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CliHarness harness = new();
        harness.StartRunningCopy();
        using ScriptedHttpHandler handler = status == 0
            ? ScriptedHttpHandler.Throwing(new HttpRequestException("refused"))
            : ScriptedHttpHandler.Json((HttpStatusCode)status, "{}");

        int code = await harness.RunAsync(handler, ct, "devices", "revoke", "--device", "dev-1");

        Assert.Equal(1, code);
        Assert.False(string.IsNullOrWhiteSpace(harness.Console.AllText));
    }

    /// <summary>Each failing status prints its own exact message on the error stream, prints nothing on the output stream, and exits 1.</summary>
    /// <param name="status">The HTTP status the gateway answers with.</param>
    /// <param name="message">The exact line the CLI prints.</param>
    [Theory]
    [InlineData(401, "Huddle did not accept the gateway token (401). Check Team:Gateway:Auth:Token or HUDDLE_GATEWAY_TOKEN.")]
    [InlineData(404, "Not found (404): there is no such device or request.")]
    [InlineData(403, "Refused (403): the request was not allowed.")]
    [InlineData(500, "The request failed (500).")]
    public async Task Devices_FailureStatus_PrintsItsExactMessage(int status, string message)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CliHarness harness = new();
        harness.StartRunningCopy();
        using ScriptedHttpHandler handler = ScriptedHttpHandler.Json((HttpStatusCode)status, "{}");

        int code = await harness.RunAsync(handler, ct, "devices", "revoke", "--device", "dev-1");

        Assert.Equal(1, code);
        Assert.Equal(message + Environment.NewLine, harness.Console.ErrorText);
        Assert.Empty(harness.Console.OutText);
    }
}
