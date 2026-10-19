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

    private const string TwoGateText = "---\ntitle: 'Flow'\ntemplate: 'flow@1.0.0'\ngates: ['qa-evidence-before-done', 'checks-before-merge']\ncheckCommand: 'dotnet test'\n---\n# Flow\n\n## When it applies\n";

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

        Assert.Equal(FlowText, File.ReadAllText(Path.Combine(dir.Path, "Process.md")));
        Assert.Equal("Process.md created.", cut.Find(".process-step-result").TextContent.Trim());
    }

    /// <summary>Choosing the other process changes the preview, and Use this process writes that one.</summary>
    [Fact]
    public async Task SelectOther_ThenUse_WritesTheChosenProcess()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();
        Register(ctx, dir, out _);
        IRenderedComponent<ContainerFragment> cut = RenderStep(ctx);

        IReadOnlyList<IElement> options = await TeammateCardTestSupport.OpenSelectAndListOptionsAsync(cut, "Process");
        Assert.Equal(["flow", "other"], options.Select(static item => item.TextContent.Trim()).Order(StringComparer.Ordinal).ToList());
        await TeammateCardTestSupport.ClickSelectItemAsync(cut, "other");
        cut.WaitForAssertion(() => Assert.Equal(OtherText, cut.Find(".process-step-preview").TextContent));
        await cut.InvokeAsync(() => cut.Find("button.process-step-use").ClickAsync());

        Assert.Equal(OtherText, File.ReadAllText(Path.Combine(dir.Path, "Process.md")));
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

        bool parsed = ProcessDocument.TryParse(File.ReadAllText(Path.Combine(dir.Path, "Process.md")), out ProcessDocument? document);
        Assert.True(parsed);
        Assert.NotNull(document);
        Assert.Empty(document.Gates);
        Assert.Empty(document.MissingSections);
    }

    /// <summary>A Team that already has a process keeps it, and the step says so.</summary>
    [Fact]
    public async Task UseThisProcess_ProcessExists_KeepsTheFileAndSaysSo()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();
        Register(ctx, dir, out _);
        File.WriteAllText(Path.Combine(dir.Path, "Process.md"), "custom");
        IRenderedComponent<ContainerFragment> cut = RenderStep(ctx);

        await cut.InvokeAsync(() => cut.Find("button.process-step-use").ClickAsync());

        Assert.Equal("custom", File.ReadAllText(Path.Combine(dir.Path, "Process.md")));
        Assert.Equal("This Team already has a process. Edit it on the Process tab.", cut.Find(".process-step-result").TextContent.Trim());
    }

    /// <summary>The Team page adds the Process tab last for a Team whose setup file names a process, and shows the tab at its route.</summary>
    [Fact]
    public async Task TeamPage_TeamWithProcess_HasProcessTabAndRendersIt()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();
        Register(ctx, dir, out _);
        File.WriteAllText(Path.Combine(dir.Path, "Process.md"), TwoGateText);
        ctx.Services.AddSingleton<ITeamCatalog>(new FakeTeamCatalog { Teams = [new TeamSummary(Team, [], ["Nova"], true)] });
        ctx.Services.AddSingleton(TestOptions.Create());
        ctx.ComponentFactories.AddStub<TeamRoster>();
        ctx.ComponentFactories.AddStub<TeamFilesTab>();
        ctx.ComponentFactories.AddStub<TeamTasksTab>();
        ctx.ComponentFactories.AddStub<TeamSpend>();

        IRenderedComponent<TeamPage> cut = ctx.Render<TeamPage>(p => p.Add(x => x.Team, Team).Add(x => x.Tab, "process"));

        Assert.Equal(["Members", "Files", "Tasks", "Process", "Details"], cut.FindAll(".team-page-tabs .mud-tab").Select(static header => header.TextContent.Trim()).ToList());
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

        Assert.Equal("Process.md could not be created.", cut.Find(".process-step-result").TextContent.Trim());
        Assert.False(File.Exists(Path.Combine(dir.Path, "Process.md")));
    }

    /// <summary>The tab shows a gate switch per enabled gate and the NF-5 sentence beside checks-before-merge.</summary>
    [Fact]
    public async Task Tab_TwoGates_ShowsSwitchesAndTheChecksSentence()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();
        Register(ctx, dir, out _);
        File.WriteAllText(Path.Combine(dir.Path, "Process.md"), TwoGateText);

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
        File.WriteAllText(Path.Combine(dir.Path, "Process.md"), TwoGateText);

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

        Assert.Contains("This Team has no process yet", cut.Find(".process-tab").TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("div.process-tab-gate"));
    }

    /// <summary>Edit opens Process.md in the Library page.</summary>
    [Fact]
    public async Task Tab_Edit_NavigatesToTheLibraryFile()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();
        Register(ctx, dir, out _);
        File.WriteAllText(Path.Combine(dir.Path, "Process.md"), TwoGateText);
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        await cut.InvokeAsync(() => cut.Find("button.process-tab-edit").ClickAsync());

        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();
        Assert.Equal("library?root=teams&path=Web%20Team%2FProcess.md", nav.ToBaseRelativePath(nav.Uri));
    }

    /// <summary>Turning a gate off asks first, with the gate named and its risk; Cancel leaves the file alone.</summary>
    [Fact]
    public async Task GateSwitchOff_Cancel_LeavesTheFileUnchanged()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();
        Register(ctx, dir, out _);
        File.WriteAllText(Path.Combine(dir.Path, "Process.md"), TwoGateText);
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        _ = cut.InvokeAsync(() => cut.Find("div[data-gate='qa-evidence-before-done'] input").ChangeAsync(new ChangeEventArgs { Value = false }));
        cut.WaitForAssertion(() => Assert.Equal("Turn off QA evidence before done?", cut.Find(".mud-dialog-title").TextContent.Trim()));
        Assert.Equal("A Task can then be marked Done with no verified Evidence.", cut.Find(".mud-dialog-content").TextContent.Trim());
        Assert.Equal(TwoGateText, File.ReadAllText(Path.Combine(dir.Path, "Process.md")));
        await cut.InvokeAsync(() => ButtonNamed(cut, "Cancel").ClickAsync());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-dialog-title")));
        Assert.Equal(TwoGateText, File.ReadAllText(Path.Combine(dir.Path, "Process.md")));
        Assert.Equal(2, cut.FindAll("div.process-tab-gate").Count);
    }

    /// <summary>Turning a gate off asks through the shared <c>ConfirmDialog</c> (UX-026): a danger confirm repeating the verb "Turn off", not the stock message box.</summary>
    [Fact]
    public async Task GateSwitchOff_AsksThroughTheSharedDangerConfirm()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();
        Register(ctx, dir, out _);
        File.WriteAllText(Path.Combine(dir.Path, "Process.md"), TwoGateText);
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        _ = cut.InvokeAsync(() => cut.Find("div[data-gate='qa-evidence-before-done'] input").ChangeAsync(new ChangeEventArgs { Value = false }));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));

        ConfirmDialog confirm = cut.FindComponent<ConfirmDialog>().Instance;
        Assert.True(confirm.Danger);
        Assert.Equal("Turn off", confirm.ConfirmText);
        Assert.Empty(cut.FindAll(".mud-message-box"));
    }

    /// <summary>Confirming rewrites only the gates line and keeps every other key and the body.</summary>
    [Fact]
    public async Task GateSwitchOff_Confirm_RewritesOnlyTheGatesLine()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();
        Register(ctx, dir, out _);
        File.WriteAllText(Path.Combine(dir.Path, "Process.md"), TwoGateText);
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        _ = cut.InvokeAsync(() => cut.Find("div[data-gate='qa-evidence-before-done'] input").ChangeAsync(new ChangeEventArgs { Value = false }));
        cut.WaitForAssertion(() => Assert.Equal("Turn off QA evidence before done?", cut.Find(".mud-dialog-title").TextContent.Trim()));
        await cut.InvokeAsync(() => ButtonNamed(cut, "Turn off").ClickAsync());

        string expected = TwoGateText.Replace("gates: ['qa-evidence-before-done', 'checks-before-merge']", "gates: ['checks-before-merge']", StringComparison.Ordinal);
        cut.WaitForAssertion(() => Assert.Equal(expected, File.ReadAllText(Path.Combine(dir.Path, "Process.md"))));
        cut.WaitForAssertion(() => Assert.Equal(["checks-before-merge"], cut.FindAll("div.process-tab-gate").Select(static gate => gate.GetAttribute("data-gate")).ToList()));
    }

    /// <summary>Every hard gate has its own one-sentence risk, and the switch dialog uses them.</summary>
    [Fact]
    public void GateRisk_EachGate_HasItsSentence()
    {
        Assert.Equal("A Task can then be marked Done with no verified Evidence.", ProcessGateService.GateRisk(GateId.QaEvidenceBeforeDone));
        Assert.Equal("Code can then be merged or pushed without a passing check run on record.", ProcessGateService.GateRisk(GateId.ChecksBeforeMerge));
        Assert.Equal("Work can then start on a Task before its spec is approved.", ProcessGateService.GateRisk(GateId.SpecApprovedBeforeBuild));
        Assert.Equal("A bug can then move to Review without a test that failed first.", ProcessGateService.GateRisk(GateId.FailingTestBeforeFix));
    }

    /// <summary>WithGates replaces the gates line, keeps other lines and CRLF endings, and writes an empty list as <c>[]</c>.</summary>
    [Fact]
    public void WithGates_Rewrites_KeepsOtherLinesAndLineEndings()
    {
        string text = "---\r\ntitle: 'Flow'\r\ngates: ['qa-evidence-before-done', 'checks-before-merge']\r\nwip: 2\r\n---\r\n# Flow\r\n";

        string one = ProcessDocument.WithGates(text, [GateId.ChecksBeforeMerge]);
        string none = ProcessDocument.WithGates(text, []);

        Assert.Equal("---\r\ntitle: 'Flow'\r\ngates: ['checks-before-merge']\r\nwip: 2\r\n---\r\n# Flow\r\n", one);
        Assert.Equal("---\r\ntitle: 'Flow'\r\ngates: []\r\nwip: 2\r\n---\r\n# Flow\r\n", none);
    }

    /// <summary>A document with no gates line gets one added inside the frontmatter; text without frontmatter is returned as it was.</summary>
    [Fact]
    public void WithGates_NoGatesLine_AddsOneOrLeavesTextAlone()
    {
        string added = ProcessDocument.WithGates("---\ntitle: 'X'\n---\n# X\n", [GateId.FailingTestBeforeFix]);
        string untouched = ProcessDocument.WithGates("# No frontmatter\n", [GateId.FailingTestBeforeFix]);

        Assert.Equal("---\ntitle: 'X'\ngates: ['failing-test-before-fix']\n---\n# X\n", added);
        Assert.Equal("# No frontmatter\n", untouched);
    }

    private static void Register(MudBunitContext ctx, TempDataDir dir, out DiskTeamFolders folders)
    {
        folders = new DiskTeamFolders(dir.Path);
        ctx.Services.AddSingleton<ITeamFolders>(folders);
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
