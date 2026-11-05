using System.Globalization;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Profile;
using static Agency.Huddle.Tests.Ui.AgentProfileCardTestSupport;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The Name box of <see cref="Agency.Huddle.App.Components.Shared.AgentProfileCard"/> in Create mode: it opens with a
/// random name from <see cref="AgentNamePool"/>, and the reset button beside it draws another.
/// </summary>
public sealed class AgentProfileCardNameTests
{
    /// <summary>The current text of the Name box.</summary>
    private static string NameValue(IRenderedComponent<ContainerFragment> cut) =>
        FindInputControl(cut, "Name").QuerySelector("input")?.GetAttribute("value") ?? string.Empty;

    /// <summary>A new Agent's Name box opens with a name from the pool.</summary>
    [Fact]
    public async Task CreateMode_NameStartsAsAPoolName()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New agent");

        Assert.Contains(NameValue(cut), AgentNamePool.Names);
    }

    /// <summary>
    /// The reset button replaces the name with a different one from the pool. The pool never hands the showing
    /// name back (<c>AgentNamePoolTests.Pick_WithCurrent_NeverReturnsIt</c> pins that), so the only way this
    /// can read the same name twice is a read that beats the re-render the click queued: the click is raised
    /// on the dispatcher and not awaited, and under full-suite load the render can land after the test
    /// thread's next read. The test therefore waits for the box to change instead of reading it once.
    /// </summary>
    [Fact]
    public async Task CreateMode_ResetButton_DrawsAnotherName()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New agent");
        string first = NameValue(cut);
        await ClickButtonAsync(cut, "New random name");

        cut.WaitForAssertion(() => Assert.NotEqual(first, NameValue(cut)));

        Assert.Contains(NameValue(cut), AgentNamePool.Names);
    }

    /// <summary>
    /// A new Agent's Name box never opens with the Human's Name or Alias. Every other pool Name belongs to an Agent and
    /// the Human's Alias is the only free one, so the draw is deterministic: unfixed it can only return the Alias, fixed
    /// it falls back to a numbered Name.
    /// </summary>
    [Fact]
    public async Task CreateMode_OnlyFreePoolNameIsTheHumansAlias_OpensWithANumberedName()
    {
        await using TeamWebApplicationFactory factory = new();
        PersonaStore personas = factory.Services.GetRequiredService<PersonaStore>();
        string humanAlias = AgentNamePool.Names[0];
        for (int i = 1; i < AgentNamePool.Names.Count; i++)
        {
            _ = personas.Add(new PersonaIdentity(AgentNamePool.Names[i], "Other", string.Create(CultureInfo.InvariantCulture, $"pool{i}"), Teams: []), "You are other.");
        }

        await using MudBunitContext ctx = NewContext(factory);
        ctx.Services.AddSingleton<IHumanIdentity>(new AliasedHumanIdentity("Zed Human", humanAlias));

        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New agent");

        string shown = NameValue(cut);
        Assert.NotEqual(humanAlias, shown, StringComparer.OrdinalIgnoreCase);
        Assert.Matches(@"^.+ \d+$", shown);
    }

    /// <summary>
    /// A new Agent's Name box never opens with the Human's Name (as opposed to the Alias). Every other pool Name belongs
    /// to an Agent and the Human's Name is the only free one, so the draw is deterministic: unfixed it can only return
    /// that Name, fixed it falls back to a numbered Name.
    /// </summary>
    [Fact]
    public async Task CreateMode_OnlyFreePoolNameIsTheHumansName_OpensWithANumberedName()
    {
        await using TeamWebApplicationFactory factory = new();
        PersonaStore personas = factory.Services.GetRequiredService<PersonaStore>();
        string humanName = AgentNamePool.Names[0];
        for (int i = 1; i < AgentNamePool.Names.Count; i++)
        {
            _ = personas.Add(new PersonaIdentity(AgentNamePool.Names[i], "Other", string.Create(CultureInfo.InvariantCulture, $"pool{i}"), Teams: []), "You are other.");
        }

        await using MudBunitContext ctx = NewContext(factory);
        ctx.Services.AddSingleton<IHumanIdentity>(new AliasedHumanIdentity(humanName));

        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New agent");

        string shown = NameValue(cut);
        Assert.NotEqual(humanName, shown, StringComparer.OrdinalIgnoreCase);
        Assert.Matches(@"^.+ \d+$", shown);
    }

    /// <summary>A name typed on a draft card that is the Human's is refused on the card, with wording for the Human, and nothing is handed on.</summary>
    [Fact]
    public async Task DraftCard_NameIsTheHumans_IsRefusedOnTheCard()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        ctx.Services.AddSingleton<IHumanIdentity>(new AliasedHumanIdentity("Sam"));
        List<string> saved = [];
        DialogParameters parameters = new()
        {
            [nameof(AgentProfileCard.Mode)] = AgentProfileCardMode.Create,
            [nameof(AgentProfileCard.Name)] = "Ada",
            [nameof(AgentProfileCard.Title)] = "Helper",
            [nameof(AgentProfileCard.Text)] = "You help.",
            [nameof(AgentProfileCard.OnDraftSaved)] = EventCallback.Factory.Create<(string Name, string Title, string Text, string? Model, string? Effort, Avatar Avatar, byte[]? Image, string? ImageExtension, string? Adapter, IReadOnlyList<string> Skills)>(this, draft => saved.Add(draft.Name)),
        };
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, factory);
        await cut.InvokeAsync(() => ctx.Services.GetRequiredService<IDialogService>().ShowAsync<AgentProfileCard>("Edit draft", parameters, AgentProfileCard.Options));

        await SetTextValueAsync(cut, "Name", "Sam");
        await ClickButtonAsync(cut, "Save draft");

        Assert.Equal("'Sam' is your own name. Choose another for this Agent.", cut.WaitForElement("[role='alert']").TextContent.Trim());
        Assert.Empty(saved);
    }

    /// <summary>A suggested name saves as it stands, with no editing.</summary>
    [Fact]
    public async Task CreateMode_AddAgent_SavesTheSuggestedName()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        PersonaStore personas = factory.Services.GetRequiredService<PersonaStore>();

        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New agent");
        await SetTextValueAsync(cut, "Title", "Probe");
        await SetTextValueAsync(cut, "Alias", "zzn");
        await SetTextValueAsync(cut, "Persona text", "a body");
        string suggested = NameValue(cut);
        await ClickButtonAsync(cut, "Add agent");

        Assert.NotNull(personas.Get(suggested));
    }
}
