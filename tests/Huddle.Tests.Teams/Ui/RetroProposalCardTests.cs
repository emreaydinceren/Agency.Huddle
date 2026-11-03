using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Tests for <see cref="RetroProposalCard"/> (Task 76, RT-5): the Human sees each proposed change as a diff Huddle computed, and
/// Accept or Reject per change goes through <see cref="RetroProposalService"/>.
/// </summary>
public sealed class RetroProposalCardTests
{
    private const string Squash = "One PR per Task, squash merged.";

    /// <summary>Registers what the card injects into <paramref name="ctx"/>.</summary>
    /// <param name="fixture">The fixture whose store and service the card uses.</param>
    /// <param name="ctx">The bUnit context to register into.</param>
    private static void Register(RetroProposalFixture fixture, MudBunitContext ctx)
    {
        AgentCardAssert.RegisterRoomServices(ctx, fixture.Harness.Directory);
        ctx.Services.AddSingleton(fixture.Proposals);
        ctx.Services.AddSingleton(fixture.Service);
    }

    /// <summary>Renders the card for a room.</summary>
    /// <param name="ctx">The bUnit context.</param>
    /// <param name="roomId">The room the card watches.</param>
    private static IRenderedComponent<RetroProposalCard> RenderCard(MudBunitContext ctx, string roomId) =>
        ctx.Render<RetroProposalCard>(parameters => parameters.Add(p => p.RoomId, roomId));

    /// <summary>Each change shows its title, its target file and the diff computed from the current file.</summary>
    [Fact]
    public async Task Renders_TitleFileAndComputedDiffPerChange()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        _ = fixture.Propose(
            RetroProposalFixture.Change("1", "AGENTS.md", "One PR per Task.", Squash),
            RetroProposalFixture.Change("2", "_team.md", "keep it small", "keep it tiny"));
        await using MudBunitContext ctx = new();
        Register(fixture, ctx);

        IRenderedComponent<RetroProposalCard> cut = RenderCard(ctx, fixture.RoomId);

        cut.WaitForAssertion(() => Assert.Equal(["Title 1", "Title 2"], cut.FindAll(".retro-change-title").Select(static each => each.TextContent.Trim())));
        Assert.Equal(["AGENTS.md", "_team.md"], cut.FindAll(".retro-change-file").Select(static each => each.TextContent.Trim()));
        Assert.Equal(
            [
                "--- AGENTS.md\n+++ AGENTS.md\n@@\n Review before done.\n-One PR per Task.\n+One PR per Task, squash merged.",
                "--- _team.md\n+++ _team.md\n@@\n ---\n-Team notes: keep it small.\n+Team notes: keep it tiny.",
            ],
            cut.FindAll(".retro-change-diff").Select(static each => each.TextContent));
        Assert.Equal(2, cut.FindAll(".retro-change-accept").Count);
        Assert.Equal(2, cut.FindAll(".retro-change-decline").Count);
    }

    /// <summary>Accept on one change applies it, and only that change leaves the card.</summary>
    [Fact]
    public async Task Accept_AppliesThatChangeOnly()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        _ = fixture.Propose(
            RetroProposalFixture.Change("1", "AGENTS.md", "One PR per Task.", Squash),
            RetroProposalFixture.Change("2", "_team.md", "keep it small", "keep it tiny"));
        await using MudBunitContext ctx = new();
        Register(fixture, ctx);
        IRenderedComponent<RetroProposalCard> cut = RenderCard(ctx, fixture.RoomId);
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".retro-change").Count));

        await cut.InvokeAsync(() => cut.FindAll(".retro-change-accept")[0].ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal(["Title 2"], cut.FindAll(".retro-change-title").Select(static each => each.TextContent.Trim())));
        Assert.Equal("# Process\n\nReview before done.\n" + Squash + "\n", fixture.ProcessNow);
        Assert.Equal(RetroProposalFixture.TeamText, fixture.TeamNow);
        Assert.Equal("Title 1", Assert.Single(fixture.Retro.ReadRegister("Platform")).Finding);
    }

    /// <summary>Reject writes nothing to the files or the register and the change leaves the card.</summary>
    [Fact]
    public async Task Reject_WritesNothing()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        _ = fixture.Propose(RetroProposalFixture.Change("1", "AGENTS.md", "One PR per Task.", Squash));
        await using MudBunitContext ctx = new();
        Register(fixture, ctx);
        IRenderedComponent<RetroProposalCard> cut = RenderCard(ctx, fixture.RoomId);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".retro-change")));
        cut.Find(".retro-proposal-reason input").Change("Too strict.");

        await cut.InvokeAsync(() => cut.Find(".retro-change-decline").ClickAsync());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".retro-change")));
        Assert.Equal(RetroProposalFixture.ProcessText, fixture.ProcessNow);
        Assert.Empty(fixture.Retro.ReadRegister("Platform"));
    }

    /// <summary>UX-030 to UX-034: the card is the shared shell; the reason field sits directly above the actions row, which reads Accept then Decline (the verb for refusing a proposal, UX-032).</summary>
    [Fact]
    public async Task Card_IsTheSharedShell_ReasonAboveAcceptThenDecline()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        _ = fixture.Propose(RetroProposalFixture.Change("1", "AGENTS.md", "One PR per Task.", Squash));
        await using MudBunitContext ctx = new();
        Register(fixture, ctx);
        AgentCardAssert.RegisterRoomServices(ctx, fixture.Harness.Directory);

        IRenderedComponent<RetroProposalCard> cut = RenderCard(ctx, fixture.RoomId);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[role=region]")));
        IElement actions = AgentCardAssert.IsShell(cut, "Nova is proposing changes", "Retro changes from Nova");
        Assert.Equal(["Accept", "Decline"], AgentCardAssert.Verbs(actions));
        string html = cut.Find("[role=region]").InnerHtml;
        Assert.True(html.IndexOf("retro-proposal-reason", StringComparison.Ordinal) < html.IndexOf("retro-change-diff", StringComparison.Ordinal), "The shared reason belongs above the changes and their action rows.");
    }

    /// <summary>UX-035: the card is not rendered for an Archived Room, as with Proposal, Question and Elicitation.</summary>
    [Fact]
    public async Task ArchivedRoom_RendersNothing()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        _ = fixture.Propose(RetroProposalFixture.Change("1", "AGENTS.md", "One PR per Task.", Squash));
        await fixture.Harness.Directory.SetRoomArchivedAsync(fixture.RoomId, true, ct);
        await using MudBunitContext ctx = new();
        Register(fixture, ctx);
        AgentCardAssert.RegisterRoomServices(ctx, fixture.Harness.Directory);

        IRenderedComponent<RetroProposalCard> cut = RenderCard(ctx, fixture.RoomId);

        Assert.True(string.IsNullOrWhiteSpace(cut.Markup));
    }

    /// <summary>UX-033: while Accept is in flight its button reads "Accepting…" and Decline is disabled.</summary>
    [Fact]
    public async Task Accept_InFlight_ShowsBusyLabel()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        _ = fixture.Propose(RetroProposalFixture.Change("1", "AGENTS.md", "One PR per Task.", Squash));
        await using MudBunitContext ctx = new();
        Register(fixture, ctx);
        AgentCardAssert.RegisterRoomServices(ctx, fixture.Harness.Directory);
        IRenderedComponent<RetroProposalCard> cut = RenderCard(ctx, fixture.RoomId);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[role=region]")));
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Gate.Gate = gate;

        // Fire-and-forget: the accept parks on the gate, so awaiting Click would never return.
        _ = cut.InvokeAsync(() => cut.FindAll("button").First(static button => button.TextContent.Trim() == "Accept").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal(["Accepting…", "Decline"], AgentCardAssert.Verbs(cut.Find(".agent-card-actions"))));
        Assert.True(cut.FindAll("button").First(static button => button.TextContent.Trim() == "Decline").HasAttribute("disabled"));

        await cut.InvokeAsync(gate.SetResult);

        cut.WaitForAssertion(() => Assert.True(string.IsNullOrWhiteSpace(cut.Markup)));
    }

    /// <summary>A room whose members proposed nothing renders no card.</summary>
    [Fact]
    public async Task NoProposal_RendersNothing()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        _ = fixture.Propose(RetroProposalFixture.Change("1", "AGENTS.md", "a", "b"));
        await using MudBunitContext ctx = new();
        Register(fixture, ctx);

        IRenderedComponent<RetroProposalCard> cut = RenderCard(ctx, fixture.OtherRoomId);

        Assert.Empty(cut.FindAll(".retro-proposal-card"));
    }
}
