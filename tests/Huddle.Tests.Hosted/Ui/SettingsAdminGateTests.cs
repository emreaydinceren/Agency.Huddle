using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Spec 6.8, C-13, C-14: Settings renders only for a viewer holding <c>operator.admin</c>; anyone else sees a notice and a <b>Request admin</b>
/// button. Lives beside the other Settings page tests, which build the page from the factory's services.
/// </summary>
public sealed class SettingsAdminGateTests
{
    private static MudBunitContext NewContext(TeamWebApplicationFactory factory)
    {
        MudBunitContext ctx = UiTestHost.Create(services => services.CopyFrom(factory.Services, ServicePresets.Settings));
        ProfileTestServices.AddProfile(ctx.Services, factory);
        SkillsTestServices.AddFromFactory(ctx.Services, factory);
        return ctx;
    }

    /// <summary>An admin viewer (the context default, C-13) gets the page as it always was: the section menu and no notice.</summary>
    [Fact]
    public async Task Settings_AdminViewer_RendersTheSectionMenu()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<Settings> cut = ctx.Render<Settings>();

        Assert.Single(cut.FindAll(".settings-page nav.settings-nav"));
        Assert.Empty(cut.FindAll("[role='alert']"));
        Assert.DoesNotContain(cut.FindAll("button"), button => button.TextContent.Trim() == "Request admin");
    }

    /// <summary>A viewer with read and write but not admin gets "Settings need admin access." in an alert and no section menu.</summary>
    [Fact]
    public async Task Settings_NonAdminViewer_ShowsNoticeInsteadOfThePage()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        ctx.SignInWithScopes(GatewayScopes.Read, GatewayScopes.Write);

        IRenderedComponent<Settings> cut = ctx.Render<Settings>();

        Assert.Equal("Settings need admin access.", cut.Find("[role='alert']").TextContent.Split('\n')[0].Trim());
        Assert.Empty(cut.FindAll("nav.settings-nav"));
    }

    /// <summary><b>Request admin</b> navigates to <c>/auth?request=admin&amp;return=/settings</c> with a full page load, so the connect page can ask for the scope.</summary>
    [Fact]
    public async Task Settings_NonAdminViewer_RequestAdminNavigatesToAuthWithForceLoad()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        ctx.SignInWithScopes(GatewayScopes.Read, GatewayScopes.Write);
        IRenderedComponent<Settings> cut = ctx.Render<Settings>();

        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Request admin").Click();

        Bunit.TestDoubles.BunitNavigationManager navigation = ctx.Services.GetRequiredService<Bunit.TestDoubles.BunitNavigationManager>();
        Assert.Equal("http://localhost/auth?request=admin&return=/settings", navigation.Uri);
        Assert.True(navigation.History.Last().Options.ForceLoad);
    }
}
