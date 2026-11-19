using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using static Agency.Huddle.Tests.Ui.AgentProfileCardTestSupport;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The account-connectors switch as the real app composes it (docs/Huddle.AgentConnectors-Specifications.md T-14 and section 13):
/// the real <see cref="PersonaStore"/> reads a stored switch back, and the card's row shows only in View mode.
/// </summary>
public sealed class AgentConnectorsPageTests
{
    private const string Row = ".agent-connectors-row";

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
}
