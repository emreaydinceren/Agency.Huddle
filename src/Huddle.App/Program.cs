using Agency.Huddle.App;
using Agency.Huddle.App.Components;
using Agency.Huddle.App.Logging;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// First, and ahead of builder.Build(): service registration is resolved inside Build, and a
// constructor that throws there takes the process with it - the empty Team:Acp:Enabled that
// SqliteTeamDirectory's options binder rejects is the worked example. A provider added after that
// point would exist only in runs that did not need it. Does nothing unless Team:FileLog:Enabled is
// true, which appsettings.Development.json sets and appsettings.json leaves off.
builder.Logging.AddTeamFileLogging(builder.Configuration);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

builder.Services.AddTeamServices(builder.Configuration);

// Liveness only, and deliberately with zero registered checks: 200 here means the process is
// up, Kestrel is bound and the endpoint pipeline is built - nothing more. That is exactly what
// test-health.ps1 asserts from outside, and it is the one claim no test in tests/Huddle.Tests
// can make, because every one of them hosts this app in-process through
// TeamWebApplicationFactory. AddHealthChecks is required: MapHealthChecks throws at start-up
// without it.
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseAntiforgery();

app.MapStaticAssets();

// Before the components only for readability - routing prefers the literal "/health" over any
// component route whatever the order, and no component declares a catch-all route.
app.MapHealthChecks("/health");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program
{
}