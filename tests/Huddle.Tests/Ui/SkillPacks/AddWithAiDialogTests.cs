using Agency.Huddle.App.Components.Settings.Skills;
using Agency.Huddle.App.Skills.Ui;
using Agency.Huddle.App.SkillSources;
using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Agency.Huddle.Tests.Ui.SkillPacks;

/// <summary>Pins <see cref="AddWithAiDialog"/> and <see cref="CopyPathMenu"/> (Skill Packs spec §6.9.7): the task it opens
/// on, the line under each task, required fields, the prompt for each task, the path form, the copy, and that it writes
/// nothing.</summary>
public sealed class AddWithAiDialogTests : IDisposable
{
    private readonly SkillsUiFixture fixture = new();

    /// <summary>Disposes the fixture.</summary>
    public void Dispose()
    {
        this.fixture.Dispose();
    }

    /// <summary>Opening with <c>Task="update"</c> selects Check for updates, which shows the All Sources switch.</summary>
    [Fact]
    public async Task OpenUpdate_SelectsCheckForUpdates()
    {
        await this.fixture.SeedAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = await OpenAsync(ctx, "update");

        Assert.NotEmpty(cut.FindAll(".add-with-ai-all-sources"));
        Assert.Empty(cut.FindAll(".add-with-ai-where"));
        Assert.Equal(ctx.Services.GetRequiredService<SkillPromptBuilder>().Update(["local/test"]), PromptText(cut));
    }

    /// <summary>Each task has its line under it.</summary>
    [Fact]
    public async Task EachTask_HasItsLine()
    {
        await this.fixture.SeedAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = await OpenAsync(ctx, "add");

        Assert.Equal(
            [
                "Your AI tool fetches what you point to, writes the Pack or catalog files, checks them and adds the rows.",
                "Your AI tool looks through the catalogs you already have, suggests up to three Packs for the role and asks you before it fetches anything.",
                "Your AI tool pulls newer commits for the Sources you pick and updates their files. Huddle then shows Update available where Teammates' copies differ.",
                "Your AI tool reads what Huddle reported about a row and repairs the row or the files it points to.",
            ],
            cut.FindAll(".add-with-ai-task-line").Select(static line => line.TextContent.Trim()));
    }

    /// <summary>An empty Where disables the prompt and both copy buttons; filling it enables them with the Add prompt, which
    /// carries the install path, the Where text, and the purpose only when given.</summary>
    [Fact]
    public async Task Add_RequiresWhere_ThenBuildsThePrompt()
    {
        await this.fixture.SeedAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = await OpenAsync(ctx, "add");
        SkillPromptBuilder prompts = ctx.Services.GetRequiredService<SkillPromptBuilder>();

        Assert.Equal("true", cut.Find(".add-with-ai-prompt").GetAttribute("aria-disabled"));
        Assert.True(cut.Find(".add-with-ai-copy").HasAttribute("disabled"));
        Assert.True(cut.Find(".copy-path-button").HasAttribute("disabled"));

        await cut.InvokeAsync(() => cut.Find(".add-with-ai-where input").InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "https://github.com/o/r" }));

        Assert.Null(cut.Find(".add-with-ai-prompt").GetAttribute("aria-disabled"));
        Assert.False(cut.Find(".add-with-ai-copy").HasAttribute("disabled"));
        Assert.False(cut.Find(".copy-path-button").HasAttribute("disabled"));
        Assert.Equal(prompts.Add("https://github.com/o/r", null), PromptText(cut));

        await cut.InvokeAsync(() => cut.Find(".add-with-ai-purpose input").InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "For reviews" }));
        Assert.Equal(prompts.Add("https://github.com/o/r", "For reviews"), PromptText(cut));
    }

    /// <summary>Find Packs for a role names the role's title and id.</summary>
    [Fact]
    public async Task Find_NamesTheRole()
    {
        await this.fixture.SeedAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = await OpenAsync(ctx, "find", roleId: "dev");

        Assert.Equal(ctx.Services.GetRequiredService<SkillPromptBuilder>().FindForRole("dev", "Developer", null), PromptText(cut));
    }

    /// <summary>Fix a problem lists only the Broken and Not downloaded rows.</summary>
    [Fact]
    public async Task Fix_ListsOnlyBrokenAndNotDownloadedRows()
    {
        this.fixture.Host.Source();
        this.fixture.Host.Pack("documents", "docx", this.fixture.Host.CloneSkill("docx"));
        this.fixture.Host.Pack("lost", "ghost", null);
        this.fixture.Host.SkillsMd(["packs/documents.skillpack.json", "packs/lost.skillpack.json"]);
        await this.fixture.Host.ReadAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = await OpenAsync(ctx, "fix");

        await cut.InvokeAsync(() => cut.Find(".add-with-ai-row .mud-select-input").MouseDownAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));

        cut.WaitForAssertion(() => Assert.Equal(["lost title — Not downloaded"], cut.FindAll(".mud-popover-open .mud-list-item").Select(static item => item.TextContent.Trim())));
    }

    /// <summary>Choosing WSL path rewrites the path in the prompt and copies it; the choice survives re-opening in the same
    /// circuit.</summary>
    [Fact]
    public async Task WslPath_RewritesThePrompt_AndSurvivesReopening()
    {
        await this.fixture.SeedAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = await OpenAsync(ctx, "update");
        string wsl = ctx.Services.GetRequiredService<SkillPromptBuilder>().InstallFilePath(PathFormat.Wsl);

        await cut.InvokeAsync(() => cut.Find(".copy-path-button").ClickAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".copy-path-wsl")));
        await cut.InvokeAsync(() => cut.Find(".copy-path-wsl").ClickAsync());

        Assert.Contains(wsl, PromptText(cut), StringComparison.Ordinal); // contains-ok: the whole prompt is pinned by SkillPromptBuilderTests; this checks the path form
        Assert.Equal(PathFormat.Wsl, ctx.Services.GetRequiredService<PathFormatChoice>().Format);
        Assert.Equal(wsl, Assert.IsType<string>(ctx.JSInterop.Invocations["huddleClipboard.copy"][^1].Arguments[0]));
        Assert.Equal("Path copied.", ctx.Services.GetRequiredService<ISnackbar>().ShownSnackbars.Last().Message);

        await cut.InvokeAsync(() => cut.Find(".add-with-ai-close").ClickAsync());
        IRenderedComponent<ContainerFragment> again = await OpenAsync(ctx, "update", cut);
        Assert.Contains(wsl, PromptText(again), StringComparison.Ordinal); // contains-ok: as above, the path form only
    }

    /// <summary>Copy prompt calls the clipboard helper with the prompt and shows the keyed snackbar; a refused copy shows
    /// the Ctrl+C warning.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CopyPrompt_CopiesOrWarns(bool accepts)
    {
        await this.fixture.SeedAsync();
        this.fixture.ClipboardAccepts = accepts;
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = await OpenAsync(ctx, "update");
        string prompt = PromptText(cut);

        await cut.InvokeAsync(() => cut.Find(".add-with-ai-copy").ClickAsync());

        Assert.Equal(prompt, Assert.IsType<string>(Assert.Single(ctx.JSInterop.Invocations["huddleClipboard.copy"]).Arguments[0]));
        Snackbar shown = Assert.Single(ctx.Services.GetRequiredService<ISnackbar>().ShownSnackbars);
        Assert.Equal(accepts ? PromptClipboard.CopiedMessage : PromptClipboard.RefusedMessage, shown.Message);
    }

    /// <summary>The dialog writes no file: the data folder is the same before and after using every task and copying.</summary>
    [Fact]
    public async Task Dialog_WritesNothing()
    {
        await this.fixture.SeedAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();
        string before = this.Written();
        long version = this.fixture.Host.Registry.Current.Version;

        IRenderedComponent<ContainerFragment> cut = await OpenAsync(ctx, "update");
        await cut.InvokeAsync(() => cut.Find(".add-with-ai-copy").ClickAsync());
        foreach (string task in new[] { "add", "find", "fix" })
        {
            await cut.InvokeAsync(() => cut.Find($".add-with-ai-task-{(task == "find" ? "findforrole" : task)} input").ClickAsync());
        }

        Assert.Equal(before, this.Written());
        Assert.Equal(version, this.fixture.Host.Registry.Current.Version);
    }

    /// <summary>Opens the dialog in a fresh popover host, or in <paramref name="host"/>.</summary>
    private static async Task<IRenderedComponent<ContainerFragment>> OpenAsync(MudBunitContext ctx, string task, IRenderedComponent<ContainerFragment>? host = null, string? roleId = null)
    {
        IRenderedComponent<ContainerFragment> cut = host ?? ctx.RenderWithPopovers(static _ => { });
        IDialogService dialogs = ctx.Services.GetRequiredService<IDialogService>();
        await cut.InvokeAsync(() => SkillDialogs.OpenAddWithAiAsync(dialogs, task, roleId));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".add-with-ai-prompt")));
        return cut;
    }

    /// <summary>Everything the dialog could write: the Skill Sources folder, <c>Skills/</c> and <c>skill-sources.json</c>, as text.</summary>
    private string Written()
    {
        IEnumerable<string> lines = Agency.Huddle.Tests.SkillPacks.Move.MoveFixture.Tree(this.fixture.Host.Dir).Select(static pair => $"sources/{pair.Key}={pair.Value}")
            .Concat(Agency.Huddle.Tests.SkillPacks.Move.MoveFixture.Tree(this.fixture.Host.Settings.SkillsDir).Select(static pair => $"skills/{pair.Key}={pair.Value}"));
        string settings = File.Exists(this.fixture.Host.Settings.FilePath) ? File.ReadAllText(this.fixture.Host.Settings.FilePath) : "(none)";
        return string.Join("\n", lines) + "\nsettings=" + settings;
    }

    /// <summary>The prompt box's text.</summary>
    private static string PromptText(IRenderedComponent<ContainerFragment> cut)
    {
        IElement box = cut.Find(".add-with-ai-prompt");
        return box.TextContent;
    }
}
