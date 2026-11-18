using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teams;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>A save of a Playbook Prompt in Settings reaches an open Playbooks tab (Team Playbooks spec §7.3, last bullet).</summary>
public sealed class PlaybooksTabRefreshTests : IDisposable
{
    private const string MemberKey = "systemPrompt.memberPlaybook";

    private readonly TempDataDir dataDir = new();

    private readonly PromptStore prompts;

    private readonly PlaybookStore store;

    private readonly string teamsRoot;

    /// <summary>Creates a real prompt store and a Playbook store that follows its saves, over a Team folder <c>Sales</c>.</summary>
    public PlaybooksTabRefreshTests()
    {
        this.teamsRoot = Path.Combine(this.dataDir.Path, "Teams");
        _ = Directory.CreateDirectory(Path.Combine(this.teamsRoot, "Sales"));
        this.prompts = new PromptStore(this.dataDir.Options(), NullLogger<PromptStore>.Instance);
        this.store = new PlaybookStore(this.teamsRoot, new PlaybookBaseStore(Path.Combine(this.dataDir.Path, "Data")), this.prompts, new PlaybookOptions());
        this.store.FollowPromptSaves(this.prompts);
    }

    /// <summary>Removes the data directory.</summary>
    public void Dispose()
    {
        this.prompts.Dispose();
        this.dataDir.Dispose();
    }

    /// <summary>Saving the Member Playbook Prompt raises <c>Changed</c> and the open tab shows the new default; saving another Prompt raises nothing.</summary>
    [Fact]
    public async Task PromptSave_OfTheMemberPlaybook_ReRendersTheTabWithTheNewDefault()
    {
        await using MudBunitContext ctx = UiTestHost.Create(services =>
        {
            _ = services.AddSingleton<ITeamFolders>(new DiskTeamFolders(this.teamsRoot));
            _ = services.AddSingleton(this.store);
        }).WithTeamOptions(options => options.Library.Enabled = true);
        int raised = 0;
        this.store.Changed += () => raised++;
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<App.Components.Teams.PlaybooksTab>(0);
            builder.AddAttribute(1, nameof(App.Components.Teams.PlaybooksTab.Team), "Sales");
            builder.CloseComponent();
        });

        this.prompts.Save("turn.fileChanged", "- Edited: {{name}}");
        Assert.Equal(0, raised);
        this.prompts.Save(MemberKey, "## Fresh start\n\nPair every change.\n");

        Assert.Equal(1, raised);
        cut.WaitForAssertion(() => Assert.Equal("Pair every change.", cut.Find("section.playbook-section[data-kind='member'] .playbook-text p").TextContent.Trim()));
    }
}
