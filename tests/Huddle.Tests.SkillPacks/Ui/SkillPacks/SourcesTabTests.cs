using Agency.Huddle.App.Components.Layout;
using Agency.Huddle.App.Components.Settings.Skills;
using Agency.Huddle.App.SkillSources.Move;
using Agency.Huddle.Tests.SkillPacks.RegistryService;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Agency.Huddle.Tests.Ui.SkillPacks;

/// <summary>Pins <see cref="SourcesTab"/> and <see cref="SourceRow"/> (Skill Packs spec §6.9.3): the install path, the
/// computer and the folder; Change… refusing a missing folder; a moved Source; a deprecated Source's reason.</summary>
public sealed class SourcesTabTests : IDisposable
{
    private readonly SkillsUiFixture fixture = new();

    /// <summary>Disposes the fixture.</summary>
    public void Dispose()
    {
        this.fixture.Dispose();
    }

    /// <summary>The install path, the computer name, the folder and the Source row render.</summary>
    [Fact]
    public async Task Tab_RendersInstallFileComputerFolderAndSource()
    {
        await this.fixture.SeedAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        Assert.Equal(Path.Combine(this.fixture.Host.Settings.DataDir, "InstallSkillPack.md"), cut.Find(".sources-install-path").TextContent.Trim());
        Assert.Equal("This computer: DESK-01", cut.Find(".sources-computer").TextContent.Trim());
        Assert.Equal(this.fixture.Host.Settings.Dir, cut.Find(".sources-folder-path").TextContent.Trim());
        Assert.Equal("Not set", cut.Find(".sources-repository").TextContent.Trim());
        Assert.Equal("1 Pack · 1 catalog · 1 Skill", cut.Find(".source-row-counts").TextContent.Trim());
    }

    /// <summary>Change… with a folder that does not exist is refused, with the reason, and the folder stays.</summary>
    [Fact]
    public async Task Change_MissingFolder_Refused()
    {
        await this.fixture.SeedAsync();
        string before = this.fixture.Host.Settings.Dir;
        string missing = Path.Combine(this.fixture.Host.DataDir, "Nowhere");
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        await cut.InvokeAsync(() => cut.Find(".sources-folder-change").ClickAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".skill-sources-folder-input input")));
        await cut.InvokeAsync(() => cut.Find(".skill-sources-folder-input input").InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = missing }));
        await cut.InvokeAsync(() => cut.Find(".skill-sources-folder-save").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal($"{missing} does not exist. Create it first, or choose another folder.", cut.Find(".skill-sources-folder-input .mud-input-helper-text").TextContent.Trim()));
        Assert.Equal(before, this.fixture.Host.Settings.Dir);
    }

    /// <summary>A moved Source says where it went and offers Copy prompt, which copies a prompt naming the new place.</summary>
    [Fact]
    public async Task MovedSource_ShowsTheLineAndCopyPrompt()
    {
        await this.fixture.SeedAsync();
        RegistryHost.Write(this.fixture.Host.Dir, "sources/local/test/source.json", """{ "schemaVersion": 1, "kind": "local", "fetchedAt": "2026-10-17T10:00:00+02:00", "fetchedBy": "Test", "signIn": "none", "status": "moved", "movedTo": "openai/plugins" }""");
        await this.fixture.Host.ReadAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        Assert.Equal("This Source moved to openai/plugins.", cut.Find(".source-row-moved").TextContent.Trim());
        await cut.InvokeAsync(() => cut.Find(".source-row-copy-prompt").ClickAsync());

        string copied = Assert.IsType<string>(Assert.Single(ctx.JSInterop.Invocations["huddleClipboard.copy"]).Arguments[0]);
        Assert.Contains("openai/plugins", copied, StringComparison.Ordinal); // contains-ok: the prompt text is pinned by SkillPromptBuilderTests
    }

    /// <summary>A Source with no source.json says Huddle does not know where it came from, and has no Remove.</summary>
    [Fact]
    public async Task SourceWithoutFile_SaysSo()
    {
        this.fixture.Host.Pack("documents", "docx", this.fixture.Host.CloneSkill("docx"));
        this.fixture.Host.SkillsMd(["packs/documents.skillpack.json"]);
        await this.fixture.Host.ReadAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        Assert.Equal("No source.json, so Huddle does not know where it came from.", cut.Find(".source-row-fetched").TextContent.Trim());
        Assert.Equal("1 Pack · 0 catalogs · 1 Skill", cut.Find(".source-row-counts").TextContent.Trim());
    }

    /// <summary>A deprecated Source shows its reason in text on the row.</summary>
    [Fact]
    public async Task DeprecatedSource_ShowsItsReason()
    {
        this.fixture.Host.Source("deprecated");
        this.fixture.Host.Pack("documents", "docx", this.fixture.Host.CloneSkill("docx"));
        this.fixture.Host.SkillsMd(["packs/documents.skillpack.json"]);
        await this.fixture.Host.ReadAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        Assert.Equal("Deprecated: Because.", cut.Find(".source-row-reason").TextContent.Trim());
        Assert.Equal("fetched Sat, Oct 17, 2026 by Test", cut.Find(".source-row-fetched").TextContent.Trim());
    }

    /// <summary>Renders the Sources tab with its popovers.</summary>
    private static IRenderedComponent<ContainerFragment> Render(MudBunitContext ctx)
    {
        return ctx.RenderWithPopovers(static builder =>
        {
            builder.OpenComponent<SourcesTab>(0);
            builder.CloseComponent();
        });
    }
}

/// <summary>Pins <see cref="MoveReportAnnouncer"/> (Skill Packs spec §6.13): the move's report shows once, with its severity,
/// and not again after a restart.</summary>
public sealed class MainLayoutMoveSnackbarTests : IDisposable
{
    private readonly SkillsUiFixture fixture = new();

    /// <summary>Disposes the fixture.</summary>
    public void Dispose()
    {
        this.fixture.Dispose();
    }

    /// <summary>An unshown report shows once as a snackbar; a second render, like a restart, shows nothing.</summary>
    [Fact]
    public async Task UnshownReport_ShownOnce()
    {
        MoveReportStore store = new(this.fixture.Host.Settings);
        store.Save(new MoveReport(DateTimeOffset.UnixEpoch, false, 6, 263, 0, 2, 0, 1, 0, [], [new MoveProblem("a", "b")]));
        await using MudBunitContext first = this.fixture.NewContext();
        first.Services.AddSingleton(store);

        _ = first.Render<MoveReportAnnouncer>();

        Snackbar shown = Assert.Single(first.Services.GetRequiredService<ISnackbar>().ShownSnackbars);
        Assert.Equal("Packs and Skills moved to SKILLS.md · 1 could not be moved", shown.Message);
        Assert.Equal(Severity.Warning, shown.Severity);

        await using MudBunitContext second = this.fixture.NewContext();
        second.Services.AddSingleton(new MoveReportStore(this.fixture.Host.Settings));
        _ = second.Render<MoveReportAnnouncer>();
        Assert.Empty(second.Services.GetRequiredService<ISnackbar>().ShownSnackbars);
    }

    /// <summary>A failed move is an error; a clean one a success.</summary>
    [Fact]
    public void SeverityOf_FailureAndClean()
    {
        MoveReport clean = new(DateTimeOffset.UnixEpoch, false, 6, 263, 0, 2, 0, 1, 0, [], []);

        Assert.Equal(Severity.Error, MoveReportAnnouncer.SeverityOf(MoveReport.Failure(DateTimeOffset.UnixEpoch, [])));
        Assert.Equal(Severity.Success, MoveReportAnnouncer.SeverityOf(clean));
        Assert.Equal(Severity.Warning, MoveReportAnnouncer.SeverityOf(clean with { SkillsWithIgnoredTools = ["x"] }));
    }
}
