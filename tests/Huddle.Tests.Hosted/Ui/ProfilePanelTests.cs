using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Settings;
using Agency.Huddle.App.Profile;
using static Agency.Huddle.Tests.Ui.TeammateCardTestSupport;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The Settings Profile tab (<see cref="ProfilePanel"/>): the Human's card with a clickable avatar, and the
/// Name, Alias, Title and Description fields that save to <c>profile.json</c> once validated.
/// </summary>
public sealed class ProfilePanelTests
{
    /// <summary>A blank system shows the welcome line, and Save is enabled even with no edits so the default can be accepted.</summary>
    [Fact]
    public async Task Unsaved_ShowsWelcome_AndSaveIsEnabled()
    {
        await using TeamWebApplicationFactory factory = new() { SeedProfile = false };
        await using MudBunitContext ctx = NewProfileContext(factory);

        IRenderedComponent<ContainerFragment> cut = RenderPanel(ctx);

        Assert.Single(cut.FindAll(".profile-welcome"));
        Assert.False(cut.Find("button.profile-save").HasAttribute("disabled"));
    }

    /// <summary>Accepting the default on first run writes <c>profile.json</c>, so the welcome line and the redirect stop.</summary>
    [Fact]
    public async Task Unsaved_SaveWithNoEdits_SavesTheProfile()
    {
        await using TeamWebApplicationFactory factory = new() { SeedProfile = false };
        await using MudBunitContext ctx = NewProfileContext(factory);
        HumanProfileStore store = factory.Services.GetRequiredService<HumanProfileStore>();

        IRenderedComponent<ContainerFragment> cut = RenderPanel(ctx);
        await ClickAsync(cut, () => cut.Find("button.profile-save"));

        Assert.True(store.IsSaved);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".profile-welcome")));
    }

    /// <summary>Typing Name, Alias and Title then saving stores them, and the card shows the new Name and Title.</summary>
    [Fact]
    public async Task Save_StoresNameAliasAndTitle_AndTheCardShowsThem()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewProfileContext(factory);
        HumanProfileStore store = factory.Services.GetRequiredService<HumanProfileStore>();

        IRenderedComponent<ContainerFragment> cut = RenderPanel(ctx);
        await SetImmediateTextValueAsync(cut, "Name", "Alan");
        await SetImmediateTextValueAsync(cut, "Alias", "al");
        await SetImmediateTextValueAsync(cut, "Title", "Site Reliability Engineer");
        await ClickAsync(cut, () => cut.Find("button.profile-save"));

        Assert.Equal(new HumanProfile("Alan", "al", "Site Reliability Engineer", null), store.Current);
        Assert.Equal("Alan", cut.Find(".roster-name").TextContent.Trim());
        Assert.Equal("Site Reliability Engineer", cut.Find(".roster-title").TextContent.Trim());
    }

    /// <summary>A Name a Teammate already has is refused with a visible problem, and nothing is saved.</summary>
    [Fact]
    public async Task Save_NameOfATeammate_ShowsTheProblemAndSavesNothing()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        await using MudBunitContext ctx = NewProfileContext(factory);
        HumanProfileStore store = factory.Services.GetRequiredService<HumanProfileStore>();

        IRenderedComponent<ContainerFragment> cut = RenderPanel(ctx);
        await SetImmediateTextValueAsync(cut, "Name", "COO");
        await ClickAsync(cut, () => cut.Find("button.profile-save"));

        Assert.Equal("Name 'COO' already belongs to the Teammate coo.", cut.Find(".profile-problem").TextContent.Trim());
        Assert.Equal("You", store.Name);
    }

    /// <summary>Clicking the card's avatar opens the shared avatar editor, whose Save writes the Human's avatar at once.</summary>
    [Fact]
    public async Task AvatarClick_OpensTheEditor_AndSaveWritesTheHumansAvatar()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewProfileContext(factory);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();

        IRenderedComponent<ContainerFragment> cut = RenderPanel(ctx);
        await OpenAvatarEditorAsync(cut);
        Assert.True(HasEditorButton(cut, "Save"));

        await SelectAvatarChoiceAsync(cut, "A short label");
        await SetImmediateTextValueAsync(cut, "Label", "ME");
        await ClickEditorButtonAsync(cut, "Save");

        cut.WaitForAssertion(() => Assert.Equal("ME", avatars.Get("You").Label), TimeSpan.FromSeconds(10));
    }

    /// <summary>A context with what <see cref="ProfilePanel"/> and the avatar dialog inject, from <paramref name="factory"/>.</summary>
    /// <param name="factory">The factory whose services back the context.</param>
    private static MudBunitContext NewProfileContext(TeamWebApplicationFactory factory)
    {
        MudBunitContext ctx = new();
        ProfileTestServices.AddProfile(ctx.Services, factory);
        return ctx;
    }

    /// <summary>Renders the panel inside the popover and dialog providers.</summary>
    /// <param name="ctx">The context.</param>
    private static IRenderedComponent<ContainerFragment> RenderPanel(MudBunitContext ctx) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<ProfilePanel>(0);
            builder.CloseComponent();
        });
}
