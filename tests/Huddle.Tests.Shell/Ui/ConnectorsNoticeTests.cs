using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Data;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The one-time notice on the Agents page: shown only when an Agent existed at upgrade, dismissible, and gone for good once dismissed
/// (docs/Huddle.AgentConnectors-Specifications.md section 13, OQ-3).
/// </summary>
public sealed class ConnectorsNoticeTests
{
    /// <summary>With an Agent present at the first start, the notice is shown.</summary>
    [Fact]
    public async Task Decide_AgentExists_ShowsTheNotice()
    {
        using TempDataDir dir = new();
        ConnectorsNoticeStore notice = new(dir.Options());
        using App.Acp.PersonaStore personas = dir.CreatePersonaStore();
        _ = personas.Add(TestPersonas.Identity("nova"), "You are Nova.");

        await new ConnectorsNoticeDecider(personas, notice).StartAsync(TestContext.Current.CancellationToken);

        Assert.True(notice.IsShown);
    }

    /// <summary>A fresh install, with no Agent yet, has nothing to be told it lost.</summary>
    [Fact]
    public async Task Decide_NoAgent_ShowsNothing()
    {
        using TempDataDir dir = new();
        ConnectorsNoticeStore notice = new(dir.Options());
        using App.Acp.PersonaStore personas = dir.CreatePersonaStore();

        await new ConnectorsNoticeDecider(personas, notice).StartAsync(TestContext.Current.CancellationToken);

        Assert.False(notice.IsShown);
    }

    /// <summary>Dismissing is stored: a later start, even with Agents, does not bring it back.</summary>
    [Fact]
    public async Task Dismiss_IsStored_AndALaterStartDoesNotReshow()
    {
        using TempDataDir dir = new();
        ConnectorsNoticeStore notice = new(dir.Options());
        using App.Acp.PersonaStore personas = dir.CreatePersonaStore();
        _ = personas.Add(TestPersonas.Identity("nova"), "You are Nova.");
        ConnectorsNoticeDecider decider = new(personas, notice);
        await decider.StartAsync(TestContext.Current.CancellationToken);

        notice.Dismiss();
        await decider.StartAsync(TestContext.Current.CancellationToken);

        Assert.False(new ConnectorsNoticeStore(dir.Options()).IsShown);
    }
}
