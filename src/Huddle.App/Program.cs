using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Cli;
using Agency.Huddle.App.Components;
using Agency.Huddle.App.Desktop;
using Agency.Huddle.App.Gateway;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Logging;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using MudBlazor.Services;

// "Agency.Huddle.App.exe stop" is a client verb, not a host: it never reaches CreateBuilder. The launcher
// always sets Team__DataDir, so the environment (not configuration) is the source of the data dir here.
if (args is ["stop"])
{
    string stopDataDir = Path.GetFullPath(
        Environment.GetEnvironmentVariable("Team__DataDir") is { Length: > 0 } stopDir ? stopDir : "App_Data");
    return await DesktopStopClient.RunAsync(
        stopDataDir, null, Console.Out, TimeSpan.FromSeconds(45), CancellationToken.None).ConfigureAwait(false);
}

// Any other bare first word is a CLI verb (auth-token, dashboard, devices) or a mistake: either way it never
// starts the server, and an unknown one prints the usage and exits 2 (C-14). Options such as --urls=... and
// Team:Key=value start with a dash or contain "=", so a normal launch is untouched.
if (CliVerbs.Handles(args))
{
    string verbDataDir = Path.GetFullPath(
        Environment.GetEnvironmentVariable("Team__DataDir") is { Length: > 0 } verbDir ? verbDir : "App_Data");
    IConfiguration verbConfiguration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true)
        .AddEnvironmentVariables()
        .Build();
    CliContext verbContext = new(
        verbDataDir, new SystemCliConsole(), new SystemBrowserOpener(), verbConfiguration, new ProcessGatewayEnvironment(), null);
    return await CliVerbs.RunAsync(args, verbContext, CancellationToken.None).ConfigureAwait(false);
}

// --log-level LEVEL is shorthand for the console's level of Huddle's own logs; the console shows only
// errors otherwise. A bad value is a usage error, like an unknown verb, not a silently ignored key.
if (!LogLevelArgument.TryTranslate(args, out string[]? hostArgs, out string? logLevelError))
{
    await Console.Error.WriteLineAsync(logLevelError).ConfigureAwait(false);
    return 2;
}

var builder = WebApplication.CreateBuilder(hostArgs);

// Desktop mode: one instance per data dir. The lock is held until the process ends. A second launch
// hands off to the running instance (opens its browser) and exits; the first launch removes a stale
// instance.json a crashed predecessor left behind, before anything can read it as live.
DesktopOptions desktop = builder.Configuration.GetSection(DesktopOptions.SectionName).Get<DesktopOptions>() ?? new();
string desktopDataDir = Path.GetFullPath(builder.Configuration["Team:DataDir"] ?? "App_Data");
IBrowserOpener handOffOpener = desktop.OpenBrowser ? new SystemBrowserOpener() : new NoopBrowserOpener();
HandOffResult handOff = desktop.Enabled
    ? await DesktopHandOff.AcquireOrHandOffAsync(
        desktopDataDir, handOffOpener, null, TimeSpan.FromSeconds(15), TimeSpan.FromMilliseconds(200), CancellationToken.None).ConfigureAwait(false)
    : new HandOffResult(null, false);
using InstanceLock? instanceLock = handOff.Lock;
if (desktop.Enabled)
{
    if (instanceLock is null)
    {
        return handOff.Opened ? 0 : 1;
    }

    InstanceFile.Delete(desktopDataDir);
}

// First, and ahead of builder.Build(): service registration is resolved inside Build, and a
// constructor that throws there takes the process with it - the empty Team:Acp:Enabled that
// SqliteTeamDirectory's options binder rejects is the worked example. A provider added after that
// point would exist only in runs that did not need it. Does nothing unless Team:FileLog:Enabled is
// true, which appsettings.json sets: the console shows only errors, so the file is the run's record.
builder.Logging.AddTeamFileLogging(builder.Configuration);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

builder.Services.AddTeamServices(builder.Configuration);

builder.Services.AddDesktopHost(builder.Configuration);

builder.Services.AddGateway();

// The development ready line (C-14): written through this writer, never ILogger, and only when the server
// reports an address, so a TestServer run stays quiet. Tests replace the writer.
builder.Services.AddSingleton(new DevConsoleOutput(Console.Out));
builder.Services.AddHostedService<DevConsoleAnnouncer>();

// Liveness only, and deliberately with zero registered checks: 200 here means the process is
// up, Kestrel is bound and the endpoint pipeline is built - nothing more. That is exactly what
// test-health.ps1 asserts from outside, and it is the one claim no test in tests/Huddle.Tests
// can make, because every one of them hosts this app in-process through
// TeamWebApplicationFactory. AddHealthChecks is required: MapHealthChecks throws at start-up
// without it.
builder.Services.AddHealthChecks();

var app = builder.Build();

// Right after Build() and before anything resolves PersonaStore: a hosted service is too late,
// because PersonaStore scans Agents/ in its own constructor and all hosted services are
// resolved before the first StartAsync (Spec §6.15, corrections-B2 #29). ILogger<AgentLayoutMigration>
// does not compile for a static class (CS0718), hence the named-category logger (#28).
AgentLayoutMigration.Run(
    app.Services.GetRequiredService<IOptions<TeamOptions>>(),
    app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Agency.Huddle.App.Acp.AgentLayoutMigration"));

// Gateway order (C-3): the gate classifies the ingress first, authentication then reads it, the session
// requirement refuses a request with no session, and only then does antiforgery run (it binds to the
// principal's NameIdentifier).
app.UseGatewayGate();
app.UseAuthentication();
app.UseAuthorization();
app.UseGatewaySessionRequirement();
app.UseAntiforgery();

app.MapStaticAssets();

// MapStaticAssets above is manifest-driven and serves build-time assets only, so it cannot see a
// file written at run time - an uploaded avatar would 404 through it. {DataDir} therefore needs its
// own provider on its own URL space, which is the shape roadmap item 7 already specifies for
// imported Themes.
var avatarsPath = Path.Combine(
    app.Services.GetRequiredService<IOptions<TeamOptions>>().Value.DataDir, "avatars");

// Created here, unlike appearance.json which is never created just to read from: a
// PhysicalFileProvider throws at construction when its root directory does not exist.
Directory.CreateDirectory(avatarsPath);

// Three entries, and that is the point: ".svg" is a KNOWN type to the default content-type
// provider, so ServeUnknownFileTypes=false alone would happily serve one that somehow reached this
// folder. An SVG is a document that can carry script, served same-origin by an application with no
// authentication of any kind - so the set of servable types is enumerated rather than filtered.
var avatarContentTypes = new FileExtensionContentTypeProvider(
    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".webp"] = "image/webp",    });

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(avatarsPath),
    RequestPath = "/agent-avatars",
    ContentTypeProvider = avatarContentTypes,
    ServeUnknownFileTypes = false,
    OnPrepareResponse = static context =>
    {
        // Safe because an avatar's file name is an opaque generated id: replacing an image produces
        // a new id and therefore a new URL, so a long immutable cache can never serve a stale one.
        context.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        context.Context.Response.Headers.XContentTypeOptions = "nosniff";
    },
});

// The Avatar Library is the Human's own folder, so it is mounted read-only where it lives rather than
// copied under {DataDir}; a chosen image is copied into the Agent's avatar by AvatarStore, never
// referenced, so deleting the library later breaks nothing. The folder may be unset or absent, and a
// PhysicalFileProvider throws on a missing root, so the mount is added only when the folder exists and
// is never created here. It reuses the enumerated three-type provider above, so master.json and any
// .svg answer 404. The cache is an hour and not immutable: unlike the opaque avatar ids, a library file
// name can be reused with different bytes.
AvatarLibraryLocator avatarLibrary = app.Services.GetRequiredService<AvatarLibraryLocator>();
if (avatarLibrary.Dir is { } avatarLibraryDir && Directory.Exists(avatarLibraryDir))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(avatarLibraryDir),
        RequestPath = "/avatar-library",
        ContentTypeProvider = avatarContentTypes,
        ServeUnknownFileTypes = false,
        OnPrepareResponse = static context =>
        {
            context.Context.Response.Headers.CacheControl = "public,max-age=3600";
            context.Context.Response.Headers.XContentTypeOptions = "nosniff";
        },
    });
}

// Not UseStaticFiles: see LibraryFilesEndpoint's summary for why a pinned Library root (which can
// point anywhere on disk, and can change while the app is running) rules out a single
// PhysicalFileProvider the way the avatars mount above uses one.
app.MapGet("/library-files/{rootId}/{**path}", LibraryFilesEndpoint.HandleAsync);

// Before the components only for readability - routing prefers the literal "/health" over any
// component route whatever the order, and no component declares a catch-all route.
app.MapHealthChecks("/health");

app.MapDesktopEndpoints();

// Loopback only and bearer-authenticated by the gateway token: the devices CLI verbs call these.
app.MapGatewayAdminEndpoints();

// Mapping only: the gate that makes every other route require a session is Stage C.
app.MapGatewayAuthEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync().ConfigureAwait(false);

return 0;

/// <summary>The entry-point type. It is <c>public partial</c> so the integration tests can name it in <c>WebApplicationFactory&lt;Program&gt;</c>; nothing constructs it.</summary>
public partial class Program
{
    /// <summary>Protected so the class is not constructible by callers, which is all the top-level statements need.</summary>
    protected Program()
    {
    }
}