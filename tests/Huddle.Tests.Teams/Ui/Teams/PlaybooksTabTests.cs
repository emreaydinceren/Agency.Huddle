using System.Globalization;
using AngleSharp.Dom;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Pins the Playbooks tab of a Team page (Team Playbooks spec §7.2 to §7.4): both sections, manager first, each state's chip, Huddle rules
/// caption, buttons and text, the Default updated notice with its diff, the warnings, Customize, Keep my version, Edit, and the Reset confirm.
/// </summary>
public sealed class PlaybooksTabTests : IDisposable
{
    private const string Team = "Sales";

    private const string MemberKey = "systemPrompt.memberPlaybook";

    private const string ManagerKey = "systemPrompt.managerPlaybook";

    private const string RulesCaption = "Huddle enforces these. They are always sent and can't be edited.";

    private const string UnknownBaseCaption = "Huddle can't tell which default this started from.";

    private const string NoBaseSentence = "Huddle no longer has the earlier default, so it can't show what changed.";

    private const string MemberDefault = "## How to work\n\nKeep passes short.\n";

    private const string ManagerDefault = "## Run the Team\n\nTriage first.\n";

    private const string OwnBody = "## Our way\n\nWe pair on everything.\n";

    private readonly string sandbox = Path.Combine(Path.GetTempPath(), "huddle-playbooks-tab-" + Guid.NewGuid().ToString("N"));

    private readonly string teamsRoot;

    private readonly FakePromptSource prompts = new();

    private readonly PlaybookOptions playbookOptions = new() { MaxChars = 1000 };

    private readonly PlaybookStore store;

    /// <summary>Creates the Teams root with the Team folder <c>Sales</c>, a store over it, and defaults for both kinds.</summary>
    public PlaybooksTabTests()
    {
        this.teamsRoot = Path.Combine(this.sandbox, "Teams");
        _ = Directory.CreateDirectory(Path.Combine(this.teamsRoot, Team));
        this.prompts.SetOverride(MemberKey, MemberDefault);
        this.prompts.SetOverride(ManagerKey, ManagerDefault);
        this.store = new PlaybookStore(this.teamsRoot, new PlaybookBaseStore(Path.Combine(this.sandbox, "Data")), this.prompts, this.playbookOptions);
    }

    /// <summary>Removes the sandbox.</summary>
    public void Dispose() => Directory.Delete(this.sandbox, recursive: true);

    /// <summary>The Manager tab and the Agent tab each hold one labelled <c>section</c> of their kind; the helper renders them one after the other.</summary>
    [Fact]
    public async Task EachTab_HasOneSectionOfItsKind()
    {
        await using MudBunitContext ctx = this.NewContext();

        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        Assert.Equal(2, cut.FindAll("section.playbooks-tab").Count);
        List<IElement> sections = [.. cut.FindAll("section.playbooks-tab section.playbook-section")];
        Assert.Equal(["Manager Playbook", "Agent Playbook"], sections.Select(static section => section.GetAttribute("aria-label")).ToList());
        Assert.Equal(["manager", "member"], sections.Select(static section => section.GetAttribute("data-kind")).ToList());
    }

    /// <summary>With no file a section is Default: the chip, the buttons and the rendered effective default of its kind.</summary>
    /// <param name="manager">Whether the Manager section is read.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Section_Default_ShowsTheChipTheDefaultAndCustomizeAndEditTheDefault(bool manager)
    {
        await using MudBunitContext ctx = this.NewContext();

        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        IElement section = SectionOf(cut, manager);
        Assert.Equal("Huddle default", ChipText(section));
        Assert.Equal(["Customize for this Team", "Edit the default"], ActionTexts(section));
        Assert.Empty(section.QuerySelectorAll(".playbook-updated"));
        Assert.Equal(manager ? ["Run the Team", "Triage first."] : ["How to work", "Keep passes short."], TextLines(Required(section, ".playbook-text")));
        Assert.NotEmpty(section.QuerySelectorAll(".playbook-text h2"));
    }

    /// <summary>A file based on the current default is Customized: the chip, Edit and Reset, and the file's own body rather than the default.</summary>
    /// <param name="manager">Whether the Manager section is read.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Section_Customized_ShowsTheChipTheFilesBodyAndEditAndReset(bool manager)
    {
        await using MudBunitContext ctx = this.NewContext();
        this.WriteFile(manager, "sha256:" + PlaybookBaseStore.Hash(Default(manager)), OwnBody);

        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        IElement section = SectionOf(cut, manager);
        Assert.Equal("Customized for this Team", ChipText(section));
        Assert.Equal(["Edit", "Reset to default…"], ActionTexts(section));
        Assert.Empty(section.QuerySelectorAll(".playbook-updated"));
        IElement body = Required(section, ".playbook-text");
        Assert.Equal(["Our way", "We pair on everything."], TextLines(body));
        string text = body.TextContent;
        Assert.DoesNotContain(manager ? "Triage first." : "Keep passes short.", text, StringComparison.Ordinal);
        Assert.DoesNotContain(UnknownBaseCaption, section.TextContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// A file whose default changed since is Default updated: the Info chip, Edit and Reset in the action row, the notice naming the kind
    /// with Show changes, Keep my version and Reset, and still the file's own body, never the new default.
    /// </summary>
    /// <param name="manager">Whether the Manager section is read.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Section_DefaultUpdated_ShowsTheInfoChipTheNoticeAndTheFilesBody(bool manager)
    {
        await using MudBunitContext ctx = this.NewContext();
        await this.CustomizeThenChangeTheDefaultAsync(manager);

        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        IElement section = SectionOf(cut, manager);
        IElement chip = Required(section, ".mud-chip");
        Assert.Equal("Default updated", chip.TextContent.Trim());
        Assert.Contains("status-chip-info", chip.ClassList);
        Assert.Equal(["Edit", "Reset to default…"], ActionTexts(section));
        IElement notice = Required(section, ".playbook-updated");
        Assert.Equal($"Huddle's default {(manager ? "Manager" : "Agent")} Playbook changed after this Team customized it.", Required(notice, ".mud-alert-message div").TextContent.Trim());
        Assert.Equal(["Show changes", "Keep my version", "Reset to default…"], notice.QuerySelectorAll("button").Select(static button => button.TextContent.Trim()).ToList());
        IElement body = Required(section, ".playbook-text");
        Assert.Contains(manager ? "Triage first." : "Keep passes short.", TextLines(body));
        string text = body.TextContent;
        Assert.DoesNotContain("changed words", text, StringComparison.Ordinal);
    }

    /// <summary>Show changes toggles <c>pre.playbook-diff</c> under the notice, with the unified diff from the earlier default to the new one.</summary>
    [Fact]
    public async Task ShowChanges_TogglesTheDiffUnderTheNotice()
    {
        await using MudBunitContext ctx = this.NewContext();
        await this.CustomizeThenChangeTheDefaultAsync(manager: false);
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);
        Assert.Empty(cut.FindAll("pre.playbook-diff"));

        await cut.InvokeAsync(() => ButtonIn(Required(SectionOf(cut, manager: false), ".playbook-updated"), "Show changes").ClickAsync());

        List<string> diffLines = TextLines(Required(SectionOf(cut, manager: false), ".playbook-updated pre.playbook-diff"));
        Assert.Contains("-Keep passes short.", diffLines);
        Assert.Contains("+Keep passes short, with changed words.", diffLines);

        await cut.InvokeAsync(() => ButtonIn(Required(SectionOf(cut, manager: false), ".playbook-updated"), "Show changes").ClickAsync());

        Assert.Empty(cut.FindAll("pre.playbook-diff"));
    }

    /// <summary>With no base file the notice shows the spec sentence in place of the diff and has no Show changes button.</summary>
    [Fact]
    public async Task DefaultUpdated_WithNoBaseFile_ShowsTheSentenceAndNoShowChanges()
    {
        await using MudBunitContext ctx = this.NewContext();
        this.WriteFile(manager: false, "sha256:0123456789abcdef", OwnBody);

        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        IElement notice = Required(SectionOf(cut, manager: false), ".playbook-updated");
        Assert.Equal(["Keep my version", "Reset to default…"], notice.QuerySelectorAll("button").Select(static button => button.TextContent.Trim()).ToList());
        Assert.Equal(NoBaseSentence, Required(notice, ".mud-typography-caption").TextContent.Trim());
        Assert.Empty(cut.FindAll("pre.playbook-diff"));
    }

    /// <summary>An Unknown base is a customized file: the Customized chip, Edit, Keep my version and Reset, and the caption after the body.</summary>
    /// <param name="manager">Whether the Manager section is read.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Section_UnknownBase_ShowsTheCustomizedChipTheCaptionAndKeepMyVersion(bool manager)
    {
        await using MudBunitContext ctx = this.NewContext();
        File.WriteAllText(Path.Combine(this.teamsRoot, Team, PlaybookFile.FileName(manager)), OwnBody);

        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        IElement section = SectionOf(cut, manager);
        Assert.Equal("Customized for this Team", ChipText(section));
        Assert.Equal(["Edit", "Keep my version", "Reset to default…"], ActionTexts(section));
        Assert.Empty(section.QuerySelectorAll(".playbook-updated"));
        string text = section.TextContent;
        Assert.Contains("We pair on everything.", TextLines(section));
        Assert.True(
            text.IndexOf(UnknownBaseCaption, StringComparison.Ordinal) > text.IndexOf("We pair on everything.", StringComparison.Ordinal),
            "The caption follows the body.");
    }

    /// <summary>The Huddle rules block of each section is read-only: the caption, the kind's rules rendered, and no buttons.</summary>
    /// <param name="manager">Whether the Manager section is read.</param>
    /// <param name="rulesLine">The first line of that kind's shipped rules.</param>
    [Theory]
    [InlineData(true, "These add to the Agent rules, for the Team you manage.")]
    [InlineData(false, "Huddle enforces these rules. Where the Playbook text says otherwise, these win.")]
    public async Task Rules_InEveryState_ShowTheCaptionAndTheRulesWithNoButtons(bool manager, string rulesLine)
    {
        await using MudBunitContext ctx = this.NewContext();
        this.WriteFile(manager, "sha256:" + PlaybookBaseStore.Hash(Default(manager)), OwnBody);

        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        IElement rules = Required(SectionOf(cut, manager), ".playbook-rules");
        Assert.Equal(RulesCaption, TextLines(rules)[0]);
        Assert.Equal(rulesLine, TextLines(rules)[1]);
        Assert.Empty(rules.QuerySelectorAll("button"));
    }

    /// <summary>The Huddle rules caption is the same in the Default, Customized, Default updated and Unknown base states.</summary>
    [Fact]
    public async Task RulesCaption_IsTheSameInEveryState()
    {
        await using MudBunitContext ctx = this.NewContext();
        File.WriteAllText(Path.Combine(this.teamsRoot, Team, PlaybookFile.FileName(manager: true)), OwnBody);
        this.WriteFile(manager: false, "sha256:0123456789abcdef", OwnBody);

        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        Assert.All(
            cut.FindAll(".playbook-rules"),
            static rules => Assert.Equal(RulesCaption, TextLines(rules)[0]));
    }

    /// <summary>A body over the limit shows the warning above the text, with the numbers in <c>N0</c> and invariant culture.</summary>
    [Fact]
    public async Task OverTheLimit_ShowsTheWarningWithInvariantNumbers()
    {
        await using MudBunitContext ctx = this.NewContext();
        string body = new('x', 12345);
        this.WriteFile(manager: false, "sha256:" + PlaybookBaseStore.Hash(MemberDefault), body);
        CultureInfo before = CultureInfo.CurrentCulture;
        CultureInfo beforeUi = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        CultureInfo.CurrentUICulture = new CultureInfo("de-DE");
        try
        {
            IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

            IElement section = SectionOf(cut, manager: false);
            IElement alert = section.QuerySelectorAll(".mud-alert-text-warning").Single();
            Assert.Equal("This Playbook is 12,345 characters. Agents read the first 1,000; shorten it.", alert.TextContent.Trim());
            Assert.Contains("mud-alert-text-warning", alert.ClassList);
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
            CultureInfo.CurrentUICulture = beforeUi;
        }
    }

    /// <summary>A body within the limit shows no warning.</summary>
    [Fact]
    public async Task WithinTheLimit_ShowsNoWarning()
    {
        await using MudBunitContext ctx = this.NewContext();
        this.WriteFile(manager: false, "sha256:" + PlaybookBaseStore.Hash(MemberDefault), OwnBody);

        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        Assert.DoesNotContain("This Playbook is", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>A file that cannot be read shows the error alert with its path and the default as the text. Windows only: it locks the file.</summary>
    [Fact]
    public async Task Unreadable_ShowsTheErrorAlertAndTheDefault()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows can lock a file against reading.");
        await using MudBunitContext ctx = this.NewContext();
        string path = Path.Combine(this.teamsRoot, Team, PlaybookFile.FileName(manager: false));
        File.WriteAllText(path, "Locked.\n");
        using FileStream hold = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        IElement section = SectionOf(cut, manager: false);
        IElement alert = Required(section, ".mud-alert[role='alert']");
        Assert.Equal($"Huddle couldn't read {path}. Agents get the Huddle default until it can be read.", alert.TextContent.Trim());
        Assert.Contains("mud-alert-text-error", alert.ClassList);
        Assert.Equal(["How to work", "Keep passes short."], TextLines(Required(section, ".playbook-text")));
    }

    /// <summary>With the Library off, Customize is disabled and its tooltip says why.</summary>
    [Fact]
    public async Task Customize_WithTheLibraryOff_IsDisabledWithItsTooltip()
    {
        await using MudBunitContext ctx = this.NewContext(libraryEnabled: false);
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);
        IElement section = SectionOf(cut, manager: false);

        Assert.True(ButtonIn(section, "Customize for this Team").HasAttribute("disabled"));
        await cut.InvokeAsync(() => Required(section, ".playbook-actions .mud-tooltip-root").TriggerEventAsync("onpointerenter", new Microsoft.AspNetCore.Components.Web.PointerEventArgs()));
        cut.WaitForAssertion(() => Assert.Equal("Turn on the Library to customize a Playbook.", cut.Find(".mud-popover-open").TextContent.Trim()));
    }

    /// <summary>With the Library on, Customize is enabled and has no tooltip.</summary>
    [Fact]
    public async Task Customize_WithTheLibraryOn_IsEnabled()
    {
        await using MudBunitContext ctx = this.NewContext();

        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        Assert.False(ButtonIn(SectionOf(cut, manager: false), "Customize for this Team").HasAttribute("disabled"));
        Assert.DoesNotContain("Turn on the Library to customize a Playbook.", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>Customize writes the file through the store, opens it in the Library editor and the section re-renders as Customized.</summary>
    /// <param name="manager">Whether the Manager Playbook is customized.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Customize_WritesTheFileOpensTheLibraryAndShowsCustomized(bool manager)
    {
        await using MudBunitContext ctx = this.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        await cut.InvokeAsync(() => ButtonIn(SectionOf(cut, manager), "Customize for this Team").ClickAsync());

        string fileName = PlaybookFile.FileName(manager);
        Assert.Equal(Default(manager), PlaybookFile.Parse(await File.ReadAllTextAsync(Path.Combine(this.teamsRoot, Team, fileName), Xunit.TestContext.Current.CancellationToken)).Body);
        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();
        Assert.Equal($"library?root=teams&path=Sales%2F{fileName}", nav.ToBaseRelativePath(nav.Uri));
        cut.WaitForAssertion(() => Assert.Equal("Customized for this Team", ChipText(SectionOf(cut, manager))));
        Assert.Equal(["Edit", "Reset to default…"], ActionTexts(SectionOf(cut, manager)));
    }

    /// <summary>A file written meanwhile is opened by Customize, not overwritten.</summary>
    [Fact]
    public async Task Customize_WithAFileWrittenMeanwhile_OpensItWithoutOverwriting()
    {
        await using MudBunitContext ctx = this.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);
        string path = Path.Combine(this.teamsRoot, Team, PlaybookFile.FileName(manager: false));
        this.WriteFile(manager: false, "sha256:" + PlaybookBaseStore.Hash(MemberDefault), OwnBody);
        string before = await File.ReadAllTextAsync(path, Xunit.TestContext.Current.CancellationToken);

        await cut.InvokeAsync(() => ButtonIn(SectionOf(cut, manager: false), "Customize for this Team").ClickAsync());

        Assert.Equal(before, await File.ReadAllTextAsync(path, Xunit.TestContext.Current.CancellationToken));
        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();
        Assert.Equal("library?root=teams&path=Sales%2F_member-playbook.md", nav.ToBaseRelativePath(nav.Uri));
    }

    /// <summary>Edit opens the Team's Playbook file of that kind in the Library editor.</summary>
    /// <param name="manager">Whether the Manager Playbook is edited.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Edit_NavigatesToTheLibraryFile(bool manager)
    {
        await using MudBunitContext ctx = this.NewContext();
        this.WriteFile(manager, "sha256:" + PlaybookBaseStore.Hash(Default(manager)), OwnBody);
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        await cut.InvokeAsync(() => ButtonIn(SectionOf(cut, manager), "Edit").ClickAsync());

        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();
        Assert.Equal($"library?root=teams&path=Sales%2F{PlaybookFile.FileName(manager)}", nav.ToBaseRelativePath(nav.Uri));
    }

    /// <summary>Edit the default goes to Settings › Prompts with that kind's Prompt highlighted, the address the Helpers page links to.</summary>
    /// <param name="manager">Whether the Manager default is edited.</param>
    /// <param name="key">The Prompt key of that kind.</param>
    [Theory]
    [InlineData(true, ManagerKey)]
    [InlineData(false, MemberKey)]
    public async Task EditTheDefault_NavigatesToSettingsPrompts(bool manager, string key)
    {
        await using MudBunitContext ctx = this.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        await cut.InvokeAsync(() => ButtonIn(SectionOf(cut, manager), "Edit the default").ClickAsync());

        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();
        Assert.Equal($"settings/prompts?highlight={key}", nav.ToBaseRelativePath(nav.Uri));
    }

    /// <summary>Keep my version restamps <c>basedOn</c> to the current default, keeps the body, and the section re-renders as Customized without the notice.</summary>
    [Fact]
    public async Task KeepMyVersion_RestampsTheFileAndShowsCustomized()
    {
        await using MudBunitContext ctx = this.NewContext();
        await this.CustomizeThenChangeTheDefaultAsync(manager: false);
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        await cut.InvokeAsync(() => ButtonIn(Required(SectionOf(cut, manager: false), ".playbook-updated"), "Keep my version").ClickAsync());

        PlaybookFile file = PlaybookFile.Parse(await File.ReadAllTextAsync(Path.Combine(this.teamsRoot, Team, PlaybookFile.FileName(manager: false)), Xunit.TestContext.Current.CancellationToken));
        Assert.Equal("sha256:" + PlaybookBaseStore.Hash(this.prompts.Raw(MemberKey)), file.BasedOn);
        Assert.Equal(MemberDefault, file.Body);
        cut.WaitForAssertion(() => Assert.Equal("Customized for this Team", ChipText(SectionOf(cut, manager: false))));
        Assert.Empty(cut.FindAll(".playbook-updated"));
    }

    /// <summary>Keep my version on an Unknown base stamps the current default and the section becomes plainly Customized.</summary>
    [Fact]
    public async Task KeepMyVersion_OnAnUnknownBase_StampsTheCurrentDefault()
    {
        await using MudBunitContext ctx = this.NewContext();
        File.WriteAllText(Path.Combine(this.teamsRoot, Team, PlaybookFile.FileName(manager: false)), OwnBody);
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        await cut.InvokeAsync(() => ButtonIn(SectionOf(cut, manager: false), "Keep my version").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal(["Edit", "Reset to default…"], ActionTexts(SectionOf(cut, manager: false))));
        Assert.DoesNotContain(UnknownBaseCaption, cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>Reset asks first: the title names the kind and the Team, the message says what is deleted, and the verb is Reset in the danger colour after Cancel.</summary>
    /// <param name="manager">Whether the Manager Playbook is reset.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Reset_AsksWithTheSpecTitleMessageAndDangerVerb(bool manager)
    {
        await using MudBunitContext ctx = this.NewContext();
        this.WriteFile(manager, "sha256:" + PlaybookBaseStore.Hash(Default(manager)), OwnBody);
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);
        string kind = manager ? "Manager" : "Agent";

        _ = cut.InvokeAsync(() => ButtonIn(SectionOf(cut, manager), "Reset to default…").ClickAsync());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));
        Assert.Equal($"Reset the {kind} Playbook of Sales?", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal(
            $"This Team's own {kind} Playbook is deleted, and its Agents read the Huddle default from their next Turn.",
            cut.Find(".mud-dialog-content").TextContent.Trim());
        List<IElement> buttons = [.. cut.FindAll(".mud-dialog-actions button")];
        Assert.Equal(["Cancel", "Reset"], buttons.Select(static button => button.TextContent.Trim()).ToList());
        Assert.Contains("mud-button-filled-error", buttons[1].ClassList);
    }

    /// <summary>Confirming Reset deletes the file and the section shows the Huddle default again.</summary>
    [Fact]
    public async Task Reset_Confirm_DeletesTheFileAndShowsTheDefault()
    {
        await using MudBunitContext ctx = this.NewContext();
        this.WriteFile(manager: false, "sha256:" + PlaybookBaseStore.Hash(MemberDefault), OwnBody);
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);
        _ = cut.InvokeAsync(() => ButtonIn(SectionOf(cut, manager: false), "Reset to default…").ClickAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));

        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").Single(static button => string.Equals(button.TextContent.Trim(), "Reset", StringComparison.Ordinal)).ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal("Huddle default", ChipText(SectionOf(cut, manager: false))));
        Assert.False(File.Exists(Path.Combine(this.teamsRoot, Team, PlaybookFile.FileName(manager: false))));
    }

    /// <summary>Cancelling Reset leaves the file and the section as they were.</summary>
    [Fact]
    public async Task Reset_Cancel_LeavesTheFile()
    {
        await using MudBunitContext ctx = this.NewContext();
        this.WriteFile(manager: false, "sha256:" + PlaybookBaseStore.Hash(MemberDefault), OwnBody);
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);
        _ = cut.InvokeAsync(() => ButtonIn(SectionOf(cut, manager: false), "Reset to default…").ClickAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));

        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").Single(static button => string.Equals(button.TextContent.Trim(), "Cancel", StringComparison.Ordinal)).ClickAsync());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-dialog-title")));
        Assert.True(File.Exists(Path.Combine(this.teamsRoot, Team, PlaybookFile.FileName(manager: false))));
        Assert.Equal("Customized for this Team", ChipText(SectionOf(cut, manager: false)));
    }

    /// <summary>Reset from the Default updated notice asks the same question.</summary>
    [Fact]
    public async Task Reset_FromTheNotice_AsksTheSameQuestion()
    {
        await using MudBunitContext ctx = this.NewContext();
        await this.CustomizeThenChangeTheDefaultAsync(manager: false);
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        _ = cut.InvokeAsync(() => ButtonIn(Required(SectionOf(cut, manager: false), ".playbook-updated"), "Reset to default…").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal("Reset the Agent Playbook of Sales?", cut.Find(".mud-dialog-title").TextContent.Trim()));
    }

    /// <summary>A write the store reports (<c>Changed</c>), such as an edit saved in the Library, re-renders the tab.</summary>
    [Fact]
    public async Task StoreChanged_ReRendersTheTab()
    {
        await using MudBunitContext ctx = this.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);
        Assert.Equal("Huddle default", ChipText(SectionOf(cut, manager: false)));

        _ = await this.store.CustomizeAsync(Team, PlaybookKind.Member, Xunit.TestContext.Current.CancellationToken);

        cut.WaitForAssertion(() => Assert.Equal("Customized for this Team", ChipText(SectionOf(cut, manager: false))));
    }

    private static IRenderedComponent<ContainerFragment> RenderTab(MudBunitContext ctx) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<App.Components.Teams.PlaybooksTab>(0);
            builder.AddAttribute(1, nameof(App.Components.Teams.PlaybooksTab.Team), Team);
            builder.AddAttribute(2, nameof(App.Components.Teams.PlaybooksTab.Manager), true);
            builder.CloseComponent();
            builder.OpenComponent<App.Components.Teams.PlaybooksTab>(3);
            builder.AddAttribute(4, nameof(App.Components.Teams.PlaybooksTab.Team), Team);
            builder.AddAttribute(5, nameof(App.Components.Teams.PlaybooksTab.Manager), false);
            builder.CloseComponent();
        });

    private static IElement SectionOf(IRenderedComponent<ContainerFragment> cut, bool manager) =>
        cut.Find($"section.playbook-section[data-kind='{(manager ? "manager" : "member")}']");

    private static List<string> TextLines(IElement element) =>
        [.. element.TextContent.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    private static IElement Required(IElement root, string selector)
    {
        IElement? element = root.QuerySelector(selector);
        Assert.NotNull(element);
        return element;
    }

    private static string ChipText(IElement section) => Required(section, ".mud-chip").TextContent.Trim();

    private static List<string> ActionTexts(IElement section) =>
        [.. section.QuerySelectorAll(".playbook-actions button").Select(static button => button.TextContent.Trim())];

    private static IElement ButtonIn(IElement container, string text) =>
        container.QuerySelectorAll("button").First(button => string.Equals(button.TextContent.Trim(), text, StringComparison.Ordinal));

    private static string Default(bool manager) => manager ? ManagerDefault : MemberDefault;

    private MudBunitContext NewContext(bool libraryEnabled = true)
    {
        MudBunitContext ctx = UiTestHost.Create(services =>
        {
            _ = services.AddSingleton<ITeamFolders>(new DiskTeamFolders(this.teamsRoot));
            _ = services.AddSingleton(this.store);
        }).WithTeamOptions(options =>
        {
            options.Library.Enabled = libraryEnabled;
            options.Playbooks = this.playbookOptions;
        });
        return ctx;
    }

    private void WriteFile(bool manager, string basedOn, string body) =>
        File.WriteAllText(Path.Combine(this.teamsRoot, Team, PlaybookFile.FileName(manager)), $"---\nbasedOn: {basedOn}\n---\n{body}");

    private async Task CustomizeThenChangeTheDefaultAsync(bool manager)
    {
        PlaybookKind kind = manager ? PlaybookKind.Manager : PlaybookKind.Member;
        _ = await this.store.CustomizeAsync(Team, kind, Xunit.TestContext.Current.CancellationToken);
        this.prompts.SetOverride(manager ? ManagerKey : MemberKey, manager ? "## Run the Team\n\nTriage first, with changed words.\n" : "## How to work\n\nKeep passes short, with changed words.\n");
    }
}
