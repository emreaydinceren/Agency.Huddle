using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using static Agency.Huddle.Tests.Ui.AgentProfileCardTestSupport;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The account-connectors switch as the real app composes it (docs/Huddle.AgentConnectors-Specifications.md T-14 and section 13):
/// the real <see cref="PersonaStore"/> reads a stored switch back, the card's row shows only in View mode, and the one-time
/// notice on the Agents page shows, is dismissed, and stays dismissed.
/// </summary>
public sealed class AgentConnectorsPageTests
{
    private const string Row = ".agent-connectors-row";
    private const string Notice = ".agents-connectors-notice";

    /// <summary>The app's real service registration hands <see cref="PersonaStore"/> a connectors store: a switch set through it reads on in the next <see cref="Persona"/>.</summary>
    [Fact]
    public async Task RealRegistration_SetConnectors_ThenPersonaReadsOn()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        PersonaStore store = factory.Services.GetRequiredService<PersonaStore>();
        Assert.False(store.Get("coo")?.Connectors);

        store.SetConnectors("coo", true);

        Assert.True(store.Get("coo")?.Connectors);
        Assert.True(factory.Services.GetRequiredService<PersonaConnectorsStore>().IsOn("coo"));
    }

    /// <summary>T-14: the View card shows the Account connectors row, and the same card in Edit mode does not.</summary>
    [Fact]
    public async Task EditMode_HasNoConnectorsRow()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(Row)));
        await ClickButtonAsync(cut, "Edit");

        cut.WaitForAssertion(() => Assert.False(HasButton(cut, "Edit")));
        Assert.Empty(cut.FindAll(Row));
    }

    /// <summary>T-14: the New Agent card does not show the Account connectors row.</summary>
    [Fact]
    public async Task NewMode_HasNoConnectorsRow()
    {
        await using TeamWebApplicationFactory factory = new();
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New agent");

        cut.WaitForAssertion(() => Assert.True(HasButton(cut, "Edit avatar…")));
        Assert.Empty(cut.FindAll(Row));
    }

    /// <summary>The Agents page shows the notice's alert when the stored flag says to, because an Agent existed at the upgrade.</summary>
    [Fact]
    public async Task Notice_FlagShow_RendersTheAlert()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(Notice)));
        Assert.Equal(
            "Account connectors are now off for every Agent. An Agent that used your mail, files or calendar will not find them until you turn them on: open the Agent and use the Account connectors switch.",
            string.Join(' ', cut.Find(Notice).TextContent.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));
    }

    /// <summary>Dismissing the notice removes it, and a fresh render of the page afterwards does not bring it back.</summary>
    [Fact]
    public async Task Notice_Dismissed_StaysGoneOnAFreshComponent()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);
        var first = RenderPage(ctx, factory);
        first.WaitForAssertion(() => Assert.Single(first.FindAll(Notice)));

        await ClickAsync(first, () => first.Find(Notice + " button"));

        first.WaitForAssertion(() => Assert.Empty(first.FindAll(Notice)));
        Assert.False(factory.Services.GetRequiredService<ConnectorsNoticeStore>().IsShown);
        await using var freshCtx = NewContext(factory);
        var second = RenderPage(freshCtx, factory);
        second.WaitForAssertion(() => Assert.NotEmpty(second.FindAll(".agents-item-name")));
        Assert.Empty(second.FindAll(Notice));
    }
}
