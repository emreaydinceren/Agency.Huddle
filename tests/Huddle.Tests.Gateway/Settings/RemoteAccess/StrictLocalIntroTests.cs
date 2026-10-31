using Agency.Huddle.App.Components.Settings.RemoteAccess;
using Agency.Huddle.App.Gateway;
using Agency.Huddle.Tests.Gateway;
using Agency.Huddle.Tests.Ui;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Settings.RemoteAccess;

/// <summary>The Remote access intro changes with the running Strict local value.</summary>
public sealed class StrictLocalIntroTests : IAsyncDisposable
{
    private readonly MudBunitContext ctx = new();
    private GatewayAdminHarness? harness;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await this.ctx.DisposeAsync();
        this.harness?.Dispose();
    }

    private IRenderedComponent<ContainerFragment> Render(bool strict)
    {
        this.harness = new GatewayAdminHarness(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Team:Gateway:StrictLocal"] = strict ? "true" : "false" });
        this.ctx.Services.AddSingleton(this.harness.Service);
        this.ctx.Services.AddSingleton<IGatewayRuntimeView>(this.harness.View);
        this.ctx.Services.AddSingleton<TimeProvider>(this.harness.Clock);
        return this.ctx.RenderWithPopovers<RemoteAccessPanel>();
    }

    /// <summary>While Strict local is on, the intro says only this computer can use Huddle and how to change that.</summary>
    [Fact]
    public void StrictLocalOn_IntroSaysOnlyThisComputer()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render(strict: true);

        Assert.Equal("Only this computer can use Huddle right now. Turn off Strict local to reach it from a phone, tablet or another computer.", cut.TextOf(".settings-intro"));
    }

    /// <summary>While Strict local is off, the intro is the original sentence.</summary>
    [Fact]
    public void StrictLocalOff_IntroIsUnchanged()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render(strict: false);

        Assert.Equal("Reach Huddle from other devices. Every device needs the gateway secret and your approval.", cut.TextOf(".settings-intro"));
    }
}
