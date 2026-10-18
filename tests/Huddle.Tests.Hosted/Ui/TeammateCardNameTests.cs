using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Teammates;
using static Agency.Huddle.Tests.Ui.TeammateCardTestSupport;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The Name box of <see cref="Agency.Huddle.App.Components.Shared.TeammateCard"/> in Create mode: it opens with a
/// random name from <see cref="TeammateNamePool"/>, and the reset button beside it draws another.
/// </summary>
public sealed class TeammateCardNameTests
{
    /// <summary>The current text of the Name box.</summary>
    private static string NameValue(IRenderedComponent<ContainerFragment> cut) =>
        FindInputControl(cut, "Name").QuerySelector("input")?.GetAttribute("value") ?? string.Empty;

    /// <summary>A new Teammate's Name box opens with a name from the pool.</summary>
    [Fact]
    public async Task CreateMode_NameStartsAsAPoolName()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New teammate");

        Assert.Contains(NameValue(cut), TeammateNamePool.Names);
    }

    /// <summary>
    /// The reset button replaces the name with a different one from the pool. The pool never hands the showing
    /// name back (<c>TeammateNamePoolTests.Pick_WithCurrent_NeverReturnsIt</c> pins that), so the only way this
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
        await ClickButtonAsync(cut, "New teammate");
        string first = NameValue(cut);
        await ClickButtonAsync(cut, "New random name");

        cut.WaitForAssertion(() => Assert.NotEqual(first, NameValue(cut)));

        Assert.Contains(NameValue(cut), TeammateNamePool.Names);
    }

    /// <summary>A suggested name saves as it stands, with no editing.</summary>
    [Fact]
    public async Task CreateMode_AddTeammate_SavesTheSuggestedName()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        PersonaStore personas = factory.Services.GetRequiredService<PersonaStore>();

        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New teammate");
        await SetTextValueAsync(cut, "Title", "Probe");
        await SetTextValueAsync(cut, "Alias", "zzn");
        await SetTextValueAsync(cut, "Persona text", "a body");
        string suggested = NameValue(cut);
        await ClickButtonAsync(cut, "Add teammate");

        Assert.NotNull(personas.Get(suggested));
    }
}
