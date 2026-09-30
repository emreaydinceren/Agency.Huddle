using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Acp;
using static Agency.Huddle.Tests.Ui.TeammateCardTestSupport;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins the read-only line <see cref="Agency.Huddle.App.Components.Shared.TeammateCard"/> shows for each
/// Adapter command a Teammate offers (Commands spec, section 6.8): what the Human can run, in the
/// Adapter's own words, rendered as text, and absent when there is nothing to offer.
/// </summary>
public sealed class TeammateCardCommandsTests
{
    private static readonly AdapterCommand Compact = new("compact", "Free up context by summarizing the conversation so far", "<optional custom summarization instructions>");

    /// <summary>A command the Teammate offers is one line: a slash, its name, and the Adapter's description.</summary>
    [Fact]
    public async Task CommandLine_ShowsTheSlashNameAndDescription()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        factory.Services.GetRequiredService<PersonaCommands>().Set("coo", [Compact]);
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");

        var line = Assert.Single(cut.FindAll(".teammate-card-command"));
        Assert.Equal("/compact — Free up context by summarizing the conversation so far", line.TextContent.Trim());
    }

    /// <summary>The description is the Adapter's text, so it is rendered as text: Markdown and markup in it stay literal.</summary>
    [Fact]
    public async Task CommandLine_RendersTheDescriptionAsTextNotMarkup()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        factory.Services.GetRequiredService<PersonaCommands>().Set("coo", [new AdapterCommand("compact", "<b>bold</b> **strong**", null)]);
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");

        var line = Assert.Single(cut.FindAll(".teammate-card-command"));
        Assert.Empty(line.QuerySelectorAll("b"));
        Assert.Equal("/compact — <b>bold</b> **strong**", line.TextContent.Trim());
    }

    /// <summary>The line says how to run the command, in a tooltip, so the Human need not guess the syntax.</summary>
    [Fact]
    public async Task CommandLine_TellsTheHumanHowToRunIt()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        factory.Services.GetRequiredService<PersonaCommands>().Set("coo", [Compact]);
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");

        var line = Assert.Single(cut.FindAll(".teammate-card-command"));
        Assert.Equal("Write @coo /compact in a Room to run it", line.GetAttribute("title"));
    }

    /// <summary>A Teammate that offers nothing, which is every Teammate on a stock install, shows no line and no empty heading.</summary>
    [Fact]
    public async Task CommandLine_IsAbsent_WhenNothingIsOffered()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");

        Assert.Empty(cut.FindAll(".teammate-card-command"));
        Assert.DoesNotContain("Commands", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>An open card follows a new list without being reopened: a MudDialog freezes its parameters, so the card subscribes itself.</summary>
    [Fact]
    public async Task CommandLine_UpdatesOnCommandsChanged()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);
        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        Assert.Empty(cut.FindAll(".teammate-card-command"));

        factory.Services.GetRequiredService<PersonaCommands>().Set("coo", [Compact]);

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".teammate-card-command")));
    }

    /// <summary>Another Teammate's list changing does not put a line on this card.</summary>
    [Fact]
    public async Task CommandLine_IgnoresAnotherTeammatesList()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);
        var cut = await OpenViewCardAsync(ctx, factory, "coo");

        factory.Services.GetRequiredService<PersonaCommands>().Set("someone-else", [Compact]);

        Assert.Empty(cut.FindAll(".teammate-card-command"));
    }

    /// <summary>The card unsubscribes when it goes, because <see cref="PersonaCommands"/> outlives it and a leaked handler would keep every closed card alive.</summary>
    [Fact]
    public async Task Dispose_Unsubscribes()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        var commands = factory.Services.GetRequiredService<PersonaCommands>();
        await using var ctx = NewContext(factory);
        _ = await OpenViewCardAsync(ctx, factory, "coo");
        Assert.Equal(1, commands.SubscriberCount);

        await ctx.DisposeComponentsAsync();

        Assert.Equal(0, commands.SubscriberCount);
    }

    /// <summary>Commands are shown when reading the card, not in Edit, where the card is a form for changing the Teammate.</summary>
    [Fact]
    public async Task CommandLine_IsNotShownInEditMode()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        factory.Services.GetRequiredService<PersonaCommands>().Set("coo", [Compact]);
        await using var ctx = NewContext(factory);
        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        Assert.NotEmpty(cut.FindAll(".teammate-card-command"));

        await ClickButtonAsync(cut, "Edit");

        Assert.Empty(cut.FindAll(".teammate-card-command"));
    }
}
