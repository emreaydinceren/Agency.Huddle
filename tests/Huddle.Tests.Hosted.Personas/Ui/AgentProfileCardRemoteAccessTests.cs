using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Gateway;
using Agency.Huddle.Tests.Gateway;
using static Agency.Huddle.Tests.Ui.AgentProfileCardTestSupport;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Remote access spec 6.15 (OP-7): the Agent card's Open action launches a process on the server, so it is not drawn while remote access is
/// configured. The refusal itself, for a handler reached some other way, is pinned by <c>HostActionsTests</c>.
/// </summary>
public sealed class AgentProfileCardRemoteAccessTests
{
    private static async Task<bool> HasOpenAsync(bool configured)
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "Chief of Staff", "You keep the team honest.");
        await using MudBunitContext ctx = NewContext(factory);
        FakeRemoteAccessState state = new(configured);
        ctx.Services.AddSingleton<IRemoteAccessState>(state);
        ctx.Services.AddSingleton(new HostActions(state));

        Bunit.IRenderedComponent<Bunit.Rendering.ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "Chief of Staff");

        return HasButton(cut, "Open");
    }

    /// <summary>With remote access off the card still offers Open, unchanged.</summary>
    [Fact]
    public async Task ViewMode_RemoteAccessOff_RendersOpen() => Assert.True(await HasOpenAsync(configured: false));

    /// <summary>With remote access configured the card does not draw Open, though Edit stays.</summary>
    [Fact]
    public async Task ViewMode_RemoteAccessConfigured_DoesNotRenderOpen() => Assert.False(await HasOpenAsync(configured: true));
}
