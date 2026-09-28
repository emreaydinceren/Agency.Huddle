using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Logging;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
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

// Right after Build() and before anything resolves PersonaStore: a hosted service is too late,
// because PersonaStore scans Teammates/ in its own constructor and all hosted services are
// resolved before the first StartAsync (Spec §6.15, corrections-B2 #29). ILogger<TeammateLayoutMigration>
// does not compile for a static class (CS0718), hence the named-category logger (#28).
TeammateLayoutMigration.Run(
    app.Services.GetRequiredService<IOptions<TeamOptions>>(),
    app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Agency.Huddle.App.Acp.TeammateLayoutMigration"));

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
        [".webp"] = "image/webp",
    });

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(avatarsPath),
    RequestPath = "/teammate-avatars",
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

// Not UseStaticFiles: see LibraryFilesEndpoint's summary for why a pinned Library root (which can
// point anywhere on disk, and can change while the app is running) rules out a single
// PhysicalFileProvider the way the avatars mount above uses one.
app.MapGet("/library-files/{rootId}/{**path}", LibraryFilesEndpoint.HandleAsync);

// Before the components only for readability - routing prefers the literal "/health" over any
// component route whatever the order, and no component declares a catch-all route.
app.MapHealthChecks("/health");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program
{
}