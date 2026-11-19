using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.Components.Shared.Dialogs;
using Agency.Huddle.App.Components.Teams;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teams;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Pins Task 57 (PR-2, PR-8, NF-5): the manager's Process step (pick, preview, use, start blank) and the
/// Team page's Process tab (rendered file, Edit, gate switches that ask before turning a hard gate off).
/// </summary>
public sealed class ProcessStepTests
{
    private const string Team = "Web Team";

    private const string FlowText = "---\ntitle: 'Flow'\ntemplate: 'flow@1.0.0'\ngates: ['qa-evidence-before-done']\n---\n# Flow\n\n## When it applies\n";

    private const string OtherText = "---\ntitle: 'Other'\n---\n# Other\n";

    private const string TwoGateBody = "# Flow\n\n## When it applies\n";

    private const string FlowBody = "# Flow\n\n## When it applies\n";

    private const string Nf5Sentence = "Checks before merge only works in work modes where the adapter asks before running shell commands.";

    /// <summary>The step previews the type's default process before anything is chosen.</summary>
    [Fact]
    public async Task Step_Initial_PreviewsTheDefaultProcess()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();
        Register(ctx, dir, out _);

        IRenderedComponent<ContainerFragment> cut = RenderStep(ctx);

        Assert.Equal(FlowText, cut.Find(".process-step-preview").TextContent);
    }

    /// <summary>Use this process copies the default template into the Team folder as <c>Process.md</c> and says so.</summary>
    [Fact]
    public async Task UseThisProcess_Default_WritesProcessMd()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();
        Register(ctx, dir, out _);
        IRenderedComponent<ContainerFragment> cut = RenderStep(ctx);

        await cut.InvokeAsync(() => cut.Find("button.process-step-use").ClickAsync());

        Assert.Equal(FlowBody, File.ReadAllText(Path.Combine(dir.Path, TeamPaths.AgentsMdFileName)));
        Assert.Equal([GateId.QaEvidenceBeforeDone], GatesOf(ctx));
        Assert.Equal("AGENTS.md created.", cut.Find(".process-step-result").TextContent.Trim());
    }

    /// <summary>Choosing the other process changes the preview, and Use this process writes that one.</summary>
    [Fact]
    public async Task SelectOther_ThenUse_WritesTheChosenProcess()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();
        Register(ctx, dir, out _);
        IRenderedComponent<ContainerFragment> cut = RenderStep(ctx);

        IReadOnlyList<IElement> options = await AgentProfileCardTestSupport.OpenSelectAndListOptionsAsync(cut, "Process");
        Assert.Equal(["flow", "other"], options.Select(static item => item.TextContent.Trim()).Order(StringComparer.Ordinal).ToList());
        await AgentProfileCardTestSupport.ClickSelectItemAsync(cut, "other");
        cut.WaitForAssertion(() => Assert.Equal(OtherText, cut.Find(".process-step-preview").TextContent));
        await cut.InvokeAsync(() => cut.Find("button.process-step-use").ClickAsync());

        Assert.Equal("# Other\n", File.ReadAllText(Path.Combine(dir.Path, TeamPaths.AgentsMdFileName)));
    }

    /// <summary>Start blank writes the seven-heading outline with no gates.</summary>
    [Fact]
    public async Task StartBlank_WritesTheBlankOutline()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();
        Register(ctx, dir, out _);
        IRenderedComponent<ContainerFragment> cut = RenderStep(ctx);

        await cut.InvokeAsync(() => cut.Find("button.process-step-blank").ClickAsync());

        bool parsed = ProcessDocument.TryParse(File.ReadAllText(Path.Combine(dir.Path, TeamPaths.AgentsMdFileName)), out ProcessDocument? document);
        Assert.True(parsed);
        Assert.NotNull(document);
        Assert.Empty(GatesOf(ctx));
        Assert.Empty(document.MissingSections);
    }

    /// <summary>A Team that already has a process keeps it, and the step says so.</summary>
    [Fact]
    public async Task UseThisProcess_ProcessExists_KeepsTheFileAndSaysSo()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();
        Register(ctx, dir, out _);
        File.WriteAllText(Path.Combine(dir.Path, TeamPaths.AgentsMdFileName), "custom");
        IRenderedComponent<ContainerFragment> cut = RenderStep(ctx);

        await cut.InvokeAsync(() => cut.Find("button.process-step-use").ClickAsync());

        Assert.Equal("custom", File.ReadAllText(Path.Combine(dir.Path, TeamPaths.AgentsMdFileName)));
        Assert.Equal("This Team already has a process. Edit it on the Process tab.", cut.Find(".process-step-result").TextContent.Trim());
    }

    /// <summary>The Team page adds the Process tab last for a Team whose setup file names a process, and shows the tab at its route.</summary>
    [Fact]
    public async Task TeamPage_TeamWithProcess_HasProcessTabAndRendersIt()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();
        Register(ctx, dir, out _);
        ctx.Services.AddSingleton<ITeamCatalog>(new FakeTeamCatalog { Teams = [new TeamSummary(Team, [], ["Nova"], true)] });
        ctx.Services.AddSingleton(TestOptions.Create());
        ArrangeTwoGates(ctx, dir);
        ctx.ComponentFactories.AddStub<TeamRoster>();
        ctx.ComponentFactories.AddStub<TeamFilesTab>();
        ctx.ComponentFactories.AddStub<TeamTasksTab>();
        ctx.ComponentFactories.AddStub<TeamSpend>();
        ctx.ComponentFactories.AddStub<ProjectSpend>();

        IRenderedComponent<TeamPage> cut = ctx.Render<TeamPage>(p => p.Add(x => x.Team, Team).Add(x => x.Tab, "process"));

        Assert.Equal(["Agents", "Library", "Process", "Manager Playbook", "Agent Playbook", "Details"], cut.FindAll(".team-page-tabs .mud-tab").Select(static header => header.TextContent.Trim()).ToList());
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("div.process-tab-gate").Count));
    }

    /// <summary>A Team with no setup file cannot take a process; the step reports it and writes nothing.</summary>
    [Fact]
    public async Task UseThisProcess_TeamWithoutSetupFile_ReportsFailure()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();
        Register(ctx, dir, out DiskTeamFolders folders);
        folders.File = null;
        IRenderedComponent<ContainerFragment> cut = RenderStep(ctx);

        await cut.InvokeAsync(() => cut.Find("button.process-step-blank").ClickAsync());

        Assert.Equal("AGENTS.md could not be created.", cut.Find(".process-step-result").TextContent.Trim());
        Assert.False(File.Exists(Path.Combine(dir.Path, TeamPaths.AgentsMdFileName)));
    }

    /// <summary>The tab shows a gate switch per enabled gate and the NF-5 sentence beside checks-before-merge.</summary>
    [Fact]
    public async Task Tab_TwoGates_ShowsSwitchesAndTheChecksSentence()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();
        Register(ctx, dir, out _);
        ArrangeTwoGates(ctx, dir);

        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        Assert.Equal(["qa-evidence-before-done", "checks-before-merge"], cut.FindAll("div.process-tab-gate").Select(static gate => gate.GetAttribute("data-gate")).ToList());
        Assert.Equal(Nf5Sentence, cut.Find("div[data-gate='checks-before-merge'] .process-tab-gate-note").TextContent.Trim());
        Assert.Empty(cut.FindAll("div[data-gate='qa-evidence-before-done'] .process-tab-gate-note"));
    }

    /// <summary>The tab renders the document body as Markdown.</summary>
    [Fact]
    public async Task Tab_WithProcess_RendersTheDocument()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();
        Register(ctx, dir, out _);
        ArrangeTwoGates(ctx, dir);

        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        Assert.Equal(["Flow", "When it applies"], cut.FindAll(".process-tab-document h1, .process-tab-document h2").Select(static heading => heading.TextContent.Trim()).ToList());
    }

    /// <summary>A Team without a process file shows a notice and no switches.</summary>
    [Fact]
    public async Task Tab_NoProcess_ShowsNotice()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();
        Register(ctx, dir, out _);

        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        Assert.Equal("No process yet", cut.Find(".process-tab .empty-state-title").TextContent.Trim());
        Assert.NotEmpty(cut.FindAll(".process-tab .empty-state-icon"));
        Assert.Contains("Add one in the Team's AGENTS.md.", cut.Find(".process-tab .empty-state-text").TextContent, StringComparison.Ordinal);
        Assert.NotEmpty(cut.FindAll(".process-tab .process-tab-add"));
        Assert.Empty(cut.FindAll("div.process-tab-gate"));
    }

    /// <summary>Edit opens <c>AGENTS.md</c> in the Library page.</summary>
    [Fact]
    public async Task Tab_Edit_NavigatesToTheLibraryFile()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();
        Register(ctx, dir, out _);
        ArrangeTwoGates(ctx, dir);
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        await cut.InvokeAsync(() => cut.Find("button.process-tab-edit").ClickAsync());

        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();
        Assert.Equal("library?root=teams&path=Web%20Team%2FAGENTS.md", nav.ToBaseRelativePath(nav.Uri));
    }

    /// <summary>Turning a gate off asks first, with the gate named and its risk; Cancel leaves the file alone.</summary>
    [Fact]
    public async Task GateSwitchOff_Cancel_LeavesTheFileUnchanged()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();
        Register(ctx, dir, out _);
        ArrangeTwoGates(ctx, dir);
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        _ = cut.InvokeAsync(() => cut.Find("div[data-gate='qa-evidence-before-done'] input").ChangeAsync(new ChangeEventArgs { Value = false }));
        cut.WaitForAssertion(() => Assert.Equal("Turn off QA evidence before done?", cut.Find(".mud-dialog-title").TextContent.Trim()));
        Assert.Equal("A Task can then be marked Done with no verified Evidence.", cut.Find(".mud-dialog-content").TextContent.Trim());
        Assert.Equal([GateId.QaEvidenceBeforeDone, GateId.ChecksBeforeMerge], GatesOf(ctx));
        await cut.InvokeAsync(() => ButtonNamed(cut, "Cancel").ClickAsync());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-dialog-title")));
        Assert.Equal([GateId.QaEvidenceBeforeDone, GateId.ChecksBeforeMerge], GatesOf(ctx));
        Assert.Equal(2, cut.FindAll("div.process-tab-gate").Count);
    }

    /// <summary>Turning a gate off asks through the shared <c>ConfirmDialog</c> (UX-026): a danger confirm repeating the verb "Turn off", not the stock message box.</summary>
    [Fact]
    public async Task GateSwitchOff_AsksThroughTheSharedDangerConfirm()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();
        Register(ctx, dir, out _);
        ArrangeTwoGates(ctx, dir);
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        _ = cut.InvokeAsync(() => cut.Find("div[data-gate='qa-evidence-before-done'] input").ChangeAsync(new ChangeEventArgs { Value = false }));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));

        ConfirmDialog confirm = cut.FindComponent<ConfirmDialog>().Instance;
        Assert.True(confirm.Danger);
        Assert.Equal("Turn off", confirm.ConfirmText);
        Assert.Empty(cut.FindAll(".mud-message-box"));
    }

    /// <summary>Confirming rewrites only the gates in <c>_team.md</c> and keeps every other setting and the <c>AGENTS.md</c> prose.</summary>
    [Fact]
    public async Task GateSwitchOff_Confirm_RewritesOnlyTheGatesLine()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();
        Register(ctx, dir, out _);
        ArrangeTwoGates(ctx, dir);
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        _ = cut.InvokeAsync(() => cut.Find("div[data-gate='qa-evidence-before-done'] input").ChangeAsync(new ChangeEventArgs { Value = false }));
        cut.WaitForAssertion(() => Assert.Equal("Turn off QA evidence before done?", cut.Find(".mud-dialog-title").TextContent.Trim()));
        await cut.InvokeAsync(() => ButtonNamed(cut, "Turn off").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal([GateId.ChecksBeforeMerge], GatesOf(ctx)));
        Assert.Equal("dotnet test", SettingsOf(ctx).CheckCommand);
        Assert.Equal("flow@1.0.0", SettingsOf(ctx).Template);
        Assert.Equal(TwoGateBody, File.ReadAllText(Path.Combine(dir.Path, TeamPaths.AgentsMdFileName)));
        cut.WaitForAssertion(() => Assert.Equal(["checks-before-merge"], cut.FindAll("div.process-tab-gate").Select(static gate => gate.GetAttribute("data-gate")).ToList()));
    }

    /// <summary>Every hard gate has its own one-sentence risk, and the switch dialog uses them.</summary>
    [Fact]
    public void GateRisk_EachGate_HasItsSentence()
    {
        Assert.Equal("A Task can then be marked Done with no verified Evidence.", ProcessGateService.GateRisk(GateId.QaEvidenceBeforeDone));
        Assert.Equal("Code can then be merged or pushed without a passing check run on record.", ProcessGateService.GateRisk(GateId.ChecksBeforeMerge));
        Assert.Equal("Code can then be merged into main without the Human's Accept.", ProcessGateService.GateRisk(GateId.AcceptBeforeMerge));
        Assert.Equal("Work can then start on a Task before its spec is approved.", ProcessGateService.GateRisk(GateId.SpecApprovedBeforeBuild));
        Assert.Equal("A bug can then move on without a test that failed first.", ProcessGateService.GateRisk(GateId.FailingTestBeforeFix));
        Assert.Equal("A Task can then start, or reach Done, while a Task it is blocked by is still open.", ProcessGateService.GateRisk(GateId.BlockersDoneBeforeStart));
    }

    /// <summary>Writes the two-gate arrangement: the prose in <c>AGENTS.md</c> and the gates, check command and template in the Team file.</summary>
    private static void ArrangeTwoGates(MudBunitContext ctx, TempDataDir dir)
    {
        File.WriteAllText(Path.Combine(dir.Path, TeamPaths.AgentsMdFileName), TwoGateBody);
        DiskTeamFolders folders = Assert.IsType<DiskTeamFolders>(ctx.Services.GetRequiredService<ITeamFolders>());
        folders.File = new TeamFile(new VersionedRef("web", "1.2.0"), null, null, null, string.Empty)
        {
            Process = ProcessSettings.Empty with
            {
                Gates = [GateId.QaEvidenceBeforeDone, GateId.ChecksBeforeMerge],
                CheckCommand = "dotnet test",
                Template = "flow@1.0.0",
            },
        };
    }

    private static ProcessSettings SettingsOf(MudBunitContext ctx) =>
        Assert.IsType<DiskTeamFolders>(ctx.Services.GetRequiredService<ITeamFolders>()).File?.Process ?? ProcessSettings.Empty;

    private static IReadOnlyList<GateId> GatesOf(MudBunitContext ctx) => SettingsOf(ctx).Gates;

    private static void Register(MudBunitContext ctx, TempDataDir dir, out DiskTeamFolders folders)
    {
        folders = new DiskTeamFolders(dir.Path);
        ctx.Services.AddSingleton<ITeamFolders>(folders);
        NeedsYouLevelUiRig.AddTo(ctx, dir);
        ctx.Services.AddSingleton(ProcessProvisionerFixtures.Types(dir));
        ctx.Services.AddSingleton(ProcessProvisionerFixtures.Build(dir, folders));
    }

    private static IRenderedComponent<ContainerFragment> RenderStep(MudBunitContext ctx) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<ProcessStep>(0);
            builder.AddAttribute(1, nameof(ProcessStep.Team), Team);
            builder.CloseComponent();
        });

    private static IRenderedComponent<ContainerFragment> RenderTab(MudBunitContext ctx) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<ProcessTab>(0);
            builder.AddAttribute(1, nameof(ProcessTab.Team), Team);
            builder.CloseComponent();
        });

    private static IElement ButtonNamed(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.FindAll(".mud-dialog-actions button").Single(button => string.Equals(button.TextContent.Trim(), text, StringComparison.Ordinal));
}
