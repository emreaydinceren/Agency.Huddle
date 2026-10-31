using Agency.Huddle.App.Desktop;
using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.App.Cli;

/// <summary>Everything a CLI verb needs, so a test supplies fakes.</summary>
/// <param name="DataDir">The data directory (<c>instance.json</c>, <c>team.db</c>).</param>
/// <param name="Console">Where output goes.</param>
/// <param name="Opener">Opens the browser for <c>dashboard</c>.</param>
/// <param name="Configuration">The configuration (<c>Team:Gateway:Auth:Token</c>).</param>
/// <param name="Environment">The environment variables (<c>HUDDLE_GATEWAY_TOKEN</c>).</param>
/// <param name="Handler">The HTTP handler, or <see langword="null"/> for a real network handler.</param>
internal sealed record CliContext(
    string DataDir,
    ICliConsole Console,
    IBrowserOpener Opener,
    IConfiguration Configuration,
    IGatewayEnvironment Environment,
    HttpMessageHandler? Handler);
