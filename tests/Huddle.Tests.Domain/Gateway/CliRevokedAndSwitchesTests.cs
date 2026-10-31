using System.Net;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Manual-run fixes to the CLI verbs: a revoked device is marked (bug 7) and <c>--Section:Key=value</c> switches apply like the app's command line (bug 9).</summary>
public sealed class CliRevokedAndSwitchesTests
{
    private const string RevokedJson = """
        {"devices":[
          {"deviceId":"dev-1","name":"Pixel Tablet","scopes":["operator.read"],"pairedVia":"approval","revoked":false},
          {"deviceId":"dev-3","name":"Old Phone","scopes":["operator.read","operator.write"],"pairedVia":"approval","revoked":true}],
         "requests":[]}
        """;

    /// <summary><c>devices list</c> prints a revoked device with a trailing <c>revoked</c> marker and an active one without.</summary>
    [Fact]
    public async Task List_RevokedDevice_IsMarked()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CliHarness harness = new();
        using ScriptedHttpHandler handler = ScriptedHttpHandler.Json(HttpStatusCode.OK, RevokedJson);

        int code = await harness.RunAsync(handler, ct, "devices", "list", "--url", "http://127.0.0.1:5200");

        Assert.Equal(0, code);
        Assert.Equal(
            string.Join(
                Environment.NewLine,
                "Devices:",
                "  dev-1  Pixel Tablet  [operator.read]  approval",
                "  dev-3  Old Phone  [operator.read,operator.write]  approval  revoked",
                "Pending requests:",
                "  (none)",
                string.Empty),
            harness.Console.OutText);
    }

    /// <summary><c>--Team:DataDir=…</c> after the verb points the verb at that data folder (the running copy's record is read from there) and is not a usage error.</summary>
    [Fact]
    public async Task List_DataDirSwitch_UsesThatFolder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CliHarness other = new();
        other.StartRunningCopy("http://127.0.0.1:5399");
        using CliHarness harness = new();
        using ScriptedHttpHandler handler = ScriptedHttpHandler.Json(HttpStatusCode.OK, RevokedJson);

        int code = await harness.RunAsync(handler, ct, "devices", "list", "--Team:DataDir=" + other.DataDir);

        Assert.Equal(0, code);
        Assert.Equal("http://127.0.0.1:5399/_gateway/devices", Assert.Single(handler.Requests).Uri.AbsoluteUri);
    }

    /// <summary><c>--Team:Gateway:Auth:Token=…</c> is the token the verb sends, ahead of the configuration, the environment and the stored one.</summary>
    [Fact]
    public async Task List_TokenSwitch_IsTheBearer()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CliHarness harness = new();
        using ScriptedHttpHandler handler = ScriptedHttpHandler.Json(HttpStatusCode.OK, RevokedJson);
        const string Switched = "fedcba9876543210fedcba9876543210fedcba9876543210";

        int code = await harness.RunAsync(
            handler, ct, "devices", "list", "--url", "http://127.0.0.1:5200", "--Team:Gateway:Auth:Token=" + Switched);

        Assert.Equal(0, code);
        Assert.Equal("Bearer " + Switched, Assert.Single(handler.Requests).Authorization);
    }

    /// <summary>A plain unknown option (no <c>Section:Key</c> form) is still a usage error, exit 2, so the switch handling does not swallow typos.</summary>
    [Fact]
    public async Task List_UnknownPlainOption_StillPrintsUsage()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CliHarness harness = new();
        using ScriptedHttpHandler handler = ScriptedHttpHandler.Json(HttpStatusCode.OK, RevokedJson);

        int code = await harness.RunAsync(handler, ct, "devices", "list", "--bogus", "x");

        Assert.Equal(2, code);
        Assert.Empty(handler.Requests);
    }
}
