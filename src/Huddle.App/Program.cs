using Agency.Huddle.App;
using Agency.Huddle.App.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddTeamServices(builder.Configuration);

var app = builder.Build();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program
{
}