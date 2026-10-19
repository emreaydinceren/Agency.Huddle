using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Sidebar;
using Agency.Huddle.App.Tabs;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Teams;
using Agency.Huddle.Tests.Ui.Layout;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Builds a <see cref="MudBunitContext"/> with the services a page needs, one <c>With...</c> call per
/// group, so a test names what it hosts instead of re-deriving a private <c>NewContext</c>.
/// </summary>
internal static class UiTestHost
{
    /// <summary>Creates a context and lets <paramref name="configure"/> register any extra services.</summary>
    /// <param name="configure">Registers services beside the ones <see cref="MudBunitContext"/> already registers; <see langword="null"/> adds none.</param>
    /// <returns>The new context. The caller disposes it with <c>await using</c>.</returns>
    internal static MudBunitContext Create(Action<IServiceCollection>? configure = null)
    {
        MudBunitContext ctx = new();
        configure?.Invoke(ctx.Services);
        return ctx;
    }

    /// <summary>Registers everything the Tasks stack of <paramref name="harness"/> provides.</summary>
    /// <param name="ctx">The context to register into.</param>
    /// <param name="harness">The harness whose services the page under test reads.</param>
    /// <returns><paramref name="ctx"/>, for chaining.</returns>
    internal static MudBunitContext WithHarness(this MudBunitContext ctx, TaskToolHarness harness)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(harness);

        harness.AddTo(ctx.Services);
        return ctx;
    }

    /// <summary>
    /// Registers a <see cref="TabService"/> unless one is already there. <c>TryAdd</c>, because
    /// <see cref="TaskToolHarness"/> registers the harness's own; a test that needs a second, different
    /// <see cref="TabService"/> registers it explicitly.
    /// </summary>
    /// <param name="ctx">The context to register into.</param>
    /// <returns><paramref name="ctx"/>, for chaining.</returns>
    internal static MudBunitContext WithTabs(this MudBunitContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        ctx.Services.TryAddSingleton<TabService>();
        return ctx;
    }

    /// <summary>Registers an <see cref="IOptions{TOptions}"/> of <see cref="TeamOptions"/> that <paramref name="configure"/> has set.</summary>
    /// <param name="ctx">The context to register into.</param>
    /// <param name="configure">Sets the flags the page under test reads, for example <c>Library.Enabled</c>.</param>
    /// <returns><paramref name="ctx"/>, for chaining.</returns>
    internal static MudBunitContext WithTeamOptions(this MudBunitContext ctx, Action<TeamOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(configure);

        TeamOptions options = new();
        configure(options);
        ctx.Services.AddSingleton(Options.Create(options));
        return ctx;
    }

    /// <summary>
    /// Registers the stub page routes (<see cref="StubPages.Routes"/>), a Right-sidebar panel catalog over
    /// <paramref name="panels"/> when any are given, and navigates to <paramref name="url"/> when one is given.
    /// </summary>
    /// <param name="ctx">The context to register into.</param>
    /// <param name="url">The address to navigate to, or <see langword="null"/> to stay where the context starts.</param>
    /// <param name="panels">The Right-sidebar panels to catalog; none registers no catalog.</param>
    /// <returns><paramref name="ctx"/>, for chaining.</returns>
    internal static MudBunitContext WithRoutes(this MudBunitContext ctx, string? url = null, params SidebarPanel[] panels)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(panels);

        ctx.Services.AddSingleton(StubPages.Routes());
        if (panels.Length > 0)
        {
            ctx.Services.AddSingleton(new SidebarPanelCatalog(panels, Options.Create(new TeamOptions())));
        }

        if (url is not null)
        {
            ctx.Services.GetRequiredService<NavigationManager>().NavigateTo(url);
        }

        return ctx;
    }

    /// <summary>
    /// Registers the chat stack a Room list or archive dialog renders over: the directory, the chat service, the
    /// events, a Team catalog, a fake gateway, health, an avatar store and a <see cref="TabService"/>.
    /// </summary>
    /// <param name="ctx">The context to register into.</param>
    /// <param name="directory">The Teammate directory.</param>
    /// <param name="chat">The chat service; its Team catalog is used when <paramref name="catalog"/> is <see langword="null"/>.</param>
    /// <param name="events">The Room events bus.</param>
    /// <param name="dir">The data directory the avatar store reads its options from; <see langword="null"/> uses default options.</param>
    /// <param name="catalog">The Team catalog; <see langword="null"/> falls back to the chat service's, then to a <see cref="FakeTeamCatalog"/>.</param>
    /// <returns><paramref name="ctx"/>, for chaining.</returns>
    internal static MudBunitContext WithChatStack(this MudBunitContext ctx, ITeamDirectory directory, ChatService chat, RoomEvents events, TempDataDir? dir = null, ITeamCatalog? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(chat);
        ArgumentNullException.ThrowIfNull(events);

        ctx.Services.AddSingleton(directory);
        ctx.Services.AddSingleton(chat);
        ctx.Services.AddSingleton(catalog ?? chat.TeamCatalog ?? new FakeTeamCatalog());
        ctx.Services.AddSingleton(events);
        ctx.Services.AddSingleton<IAgentGateway>(new FakeAgentGateway());
        ctx.Services.AddSingleton(new PersonaHealth(TimeProvider.System, NullLogger<PersonaHealth>.Instance));
        ctx.Services.AddSingleton(new AvatarStore(dir?.Options() ?? Options.Create(new TeamOptions()), NullLogger<AvatarStore>.Instance));
        ctx.Services.AddSingleton<TabService>();
        return ctx;
    }
}
