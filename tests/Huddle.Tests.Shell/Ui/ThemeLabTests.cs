using System.Text.Json;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Appearance;
using Agency.Huddle.App.Components.Settings;
using Agency.Huddle.App.Themes.Skins;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The Theme lab (decision P5-D5): its model (what an edit does to the style block, the MudBlazor palette variables,
/// the contrast badges and the exported JSON) and the page that draws it. The lab exists only in Development.
/// </summary>
public sealed class ThemeLabTests : IDisposable
{
    private const string ModulePath = "./theme-lab.js";

    private readonly TempDataDir dataDir = new();

    private readonly AppearanceStore appearance;

    /// <summary>Gives every test its own appearance store, so the lab has a selected Theme to start from.</summary>
    public ThemeLabTests() => this.appearance = new AppearanceStore(this.dataDir.Options(), NullLogger<AppearanceStore>.Instance);

    /// <summary>Removes the test's store and data folder.</summary>
    public void Dispose()
    {
        this.appearance.Dispose();
        this.dataDir.Dispose();
    }

    /// <summary>The lab starts on the Theme the Human has selected, so a first edit previews over the page as it is.</summary>
    [Fact]
    public async Task Page_StartsOnTheSelectedTheme()
    {
        this.appearance.Save("fluent-dark");
        await using MudBunitContext ctx = this.NewContext("Development");
        _ = SetupModule(ctx);

        IRenderedComponent<ContainerFragment> cut = RenderLab(ctx);

        Assert.Equal("fluent-dark", cut.Find(".mud-select input").GetAttribute("value"));
    }

    private static Skin LumenLight() =>
        SkinLoader.LoadAll().First(static skin => string.Equals(skin.Id, "lumen-light", StringComparison.Ordinal));

    /// <summary>A lab nobody has edited exports an empty object, so Export JSON holds only what differs from the skin.</summary>
    [Fact]
    public void Model_Untouched_ExportsNothing()
    {
        ThemeLabModel model = new(LumenLight());

        Assert.Equal("{}", model.ExportJson());
        Assert.Equal(SkinLoader.PaletteKeys.Count, model.Palette.Count);
        Assert.Equal(SkinTokens.All.Count, model.Tokens.Count);
    }

    /// <summary>A valid palette edit is accepted, exported, and shown as the MudBlazor CSS variable for that key.</summary>
    [Fact]
    public void SetPalette_Valid_ExportsAndPublishesTheVariable()
    {
        ThemeLabModel model = new(LumenLight());

        string? error = model.SetPalette("textPrimary", "#112233");

        Assert.Null(error);
        Assert.Equal("#112233", model.PaletteVariables["--mud-palette-text-primary"]);
        using JsonDocument exported = JsonDocument.Parse(model.ExportJson());
        Assert.Equal("#112233", exported.RootElement.GetProperty("palette").GetProperty("textPrimary").GetString());
        Assert.False(exported.RootElement.TryGetProperty("tokens", out _));
    }

    /// <summary>An unknown key or a value that is not a colour is refused with a message and leaves the value as it was.</summary>
    [Fact]
    public void SetPalette_Invalid_IsRefusedAndKeepsTheValue()
    {
        ThemeLabModel model = new(LumenLight());
        string before = model.Palette["primary"];

        Assert.NotNull(model.SetPalette("primary", "banana"));
        Assert.NotNull(model.SetPalette("noSuchKey", "#112233"));

        Assert.Equal(before, model.Palette["primary"]);
        Assert.Equal("{}", model.ExportJson());
    }

    /// <summary>A valid token edit reaches the style block and the export; setting it back to the skin's value drops it from the export.</summary>
    [Fact]
    public void SetToken_Valid_ChangesStyleAndExport_AndBackToBaseDropsIt()
    {
        ThemeLabModel model = new(LumenLight());
        string original = model.Tokens["ground.page"];

        Assert.Null(model.SetToken("ground.page", "#abcdef"));

        Assert.Contains("--hu-ground-page:#abcdef", model.StyleCss.Split(';'));
        using (JsonDocument exported = JsonDocument.Parse(model.ExportJson()))
        {
            Assert.Equal("#abcdef", exported.RootElement.GetProperty("tokens").GetProperty("ground.page").GetString());
        }

        Assert.Null(model.SetToken("ground.page", original));
        Assert.Equal("{}", model.ExportJson());
    }

    /// <summary>Each refusal says why, in words the lab shows beneath the field.</summary>
    [Fact]
    public void Refusals_SayWhy()
    {
        ThemeLabModel model = new(LumenLight());

        Assert.Equal("'noSuchKey' is not a palette key.", model.SetPalette("noSuchKey", "#112233"));
        Assert.Equal("'banana' is not a hex or rgb()/rgba() colour.", model.SetPalette("primary", " banana "));
        Assert.Equal("'no.such.token' is not a token.", model.SetToken("no.such.token", "#112233"));
        Assert.Equal("The expression is empty.", model.SetToken("ground.page", "   "));
    }

    /// <summary>A token value outside the grammar, an unknown key and a reference loop are refused and change nothing.</summary>
    [Fact]
    public void SetToken_Invalid_IsRefusedAndKeepsTheValue()
    {
        ThemeLabModel model = new(LumenLight());
        string before = model.Tokens["ground.page"];
        string style = model.StyleCss;

        Assert.NotNull(model.SetToken("ground.page", "url(javascript:alert(1))"));
        Assert.NotNull(model.SetToken("ground.page", "ref(ground.page)"));
        Assert.NotNull(model.SetToken("no.such.token", "#112233"));
        Assert.NotNull(model.SetToken("ground.page", string.Empty));

        Assert.Equal(before, model.Tokens["ground.page"]);
        Assert.Equal(style, model.StyleCss);
    }

    /// <summary>The badges follow the edits: every pair passes on the shipped skin, and text the colour of its ground fails.</summary>
    [Fact]
    public void Contrast_FollowsTheEdits()
    {
        ThemeLabModel model = new(LumenLight());
        Assert.Equal(SkinContrastPairs.All.Count, model.Contrast().Count);
        Assert.All(model.Contrast(), static result => Assert.True(result.Passes, result.Pair.Name));

        Assert.Null(model.SetPalette("textPrimary", model.Palette["surface"]));

        ThemeLabContrast onPage = Assert.Single(model.Contrast(), static result => string.Equals(result.Pair.Name, "textPrimary on ground.page", StringComparison.Ordinal));
        Assert.False(onPage.Passes);
        Assert.Equal(1.0, onPage.Ratio);
    }

    /// <summary>A token the resolver cannot evaluate shows as unmeasured rather than failing or throwing.</summary>
    [Fact]
    public void Contrast_UnmeasurableTerm_HasNoRatio()
    {
        ThemeLabModel model = new(LumenLight());

        Assert.Null(model.SetToken("ground.raised", "mud(elevation-4)"));

        Assert.Equal(
            ["textPrimary on ground.raised", "textSecondary on ground.raised", "focus on ground.raised"],
            model.Contrast().Where(static result => result.Ratio is null).Select(static result => result.Pair.Name));
    }

    /// <summary>The exported JSON is a valid skin overlay: merged into a skin file it loads without error.</summary>
    [Fact]
    public void Export_IsAValidSkinOverlay()
    {
        ThemeLabModel model = new(LumenLight());
        Assert.Null(model.SetPalette("primary", "#336699"));
        Assert.Null(model.SetToken("hover", "mix(palette.primary 10%, palette.surface)"));
        using JsonDocument exported = JsonDocument.Parse(model.ExportJson());
        string file = "{\"id\":\"lab\",\"label\":\"Lab\",\"mode\":\"light\",\"order\":99,"
            + "\"palette\":" + exported.RootElement.GetProperty("palette").GetRawText() + ","
            + "\"tokens\":" + exported.RootElement.GetProperty("tokens").GetRawText() + "}";

        IReadOnlyList<Skin> skins = SkinLoader.LoadAll(new Dictionary<string, string>(StringComparer.Ordinal) { ["lab.light.json"] = file });

        Assert.Equal("#336699", skins[0].Palette["primary"]);
    }

    /// <summary>The page lists one input per palette key and per token, and one badge per contrast pair.</summary>
    [Fact]
    public async Task Page_InDevelopment_DrawsEveryInputAndBadge()
    {
        await using MudBunitContext ctx = this.NewContext("Development");

        IRenderedComponent<ContainerFragment> cut = RenderLab(ctx);

        Assert.Equal("Theme lab", cut.Find("h1").TextContent.Trim());
        Assert.Equal(SkinLoader.PaletteKeys.Count, cut.FindAll("[data-palette-key]").Count);
        Assert.Equal(SkinTokens.All.Count, cut.FindAll("[data-token-key]").Count);
        Assert.Equal(SkinContrastPairs.All.Count, cut.FindAll(".theme-lab-contrast").Count);
    }

    /// <summary>Outside Development the page draws nothing and sends the visitor to Settings.</summary>
    [Fact]
    public async Task Page_OutsideDevelopment_RedirectsToSettings()
    {
        await using MudBunitContext ctx = this.NewContext("Production");

        IRenderedComponent<ContainerFragment> cut = RenderLab(ctx);

        Assert.Empty(cut.FindAll("[data-token-key]"));
        Assert.Equal("/settings", new Uri(ctx.Services.GetRequiredService<NavigationManager>().Uri).AbsolutePath);
    }

    /// <summary>Typing a valid token expression applies it live: the lab hands the new style block to the page script.</summary>
    [Fact]
    public async Task TokenEdit_Valid_AppliesLive()
    {
        await using MudBunitContext ctx = this.NewContext("Development");
        BunitJSModuleInterop module = SetupModule(ctx);
        IRenderedComponent<ContainerFragment> cut = RenderLab(ctx);

        await cut.InvokeAsync(() => cut.Find("[data-token-key='ground.page'] input").InputAsync("#abcdef"));

        JSRuntimeInvocation call = Assert.Single(module.Invocations["apply"]);
        Assert.Contains("--hu-ground-page:#abcdef", Assert.IsType<string>(call.Arguments[0]).Split(';'));
    }

    /// <summary>Typing an invalid expression shows the problem on that field and applies nothing.</summary>
    [Fact]
    public async Task TokenEdit_Invalid_ShowsAnErrorAndDoesNotApply()
    {
        await using MudBunitContext ctx = this.NewContext("Development");
        List<string> seen = [];
        _ = SetupModule(ctx, seen);
        IRenderedComponent<ContainerFragment> cut = RenderLab(ctx);

        await cut.InvokeAsync(() => cut.Find("[data-token-key='ground.page'] input").InputAsync("ref(ground.page)"));

        Assert.DoesNotContain("apply", seen);
        Assert.NotEmpty(cut.FindAll("[data-token-key='ground.page'] .mud-input-helper-text.mud-input-error"));
    }

    /// <summary>Export JSON copies the differing keys to the clipboard through the page script.</summary>
    [Fact]
    public async Task Export_CopiesTheJsonToTheClipboard()
    {
        await using MudBunitContext ctx = this.NewContext("Development");
        BunitJSModuleInterop module = SetupModule(ctx);
        IRenderedComponent<ContainerFragment> cut = RenderLab(ctx);
        await cut.InvokeAsync(() => cut.Find("[data-token-key='ground.page'] input").InputAsync("#abcdef"));

        await cut.InvokeAsync(() => cut.Find("#theme-lab-export").ClickAsync());

        JSRuntimeInvocation call = Assert.Single(module.Invocations["copy"]);
        using JsonDocument copied = JsonDocument.Parse(Assert.IsType<string>(call.Arguments[0]));
        Assert.Equal("#abcdef", copied.RootElement.GetProperty("tokens").GetProperty("ground.page").GetString());
    }

    /// <summary>The Inspect toggle switches the page script's hover inspection on, and leaving the page resets the preview.</summary>
    [Fact]
    public async Task Inspect_TogglesTheScript_AndDisposeResets()
    {
        await using MudBunitContext ctx = this.NewContext("Development");
        BunitJSModuleInterop module = SetupModule(ctx);
        IRenderedComponent<ContainerFragment> cut = RenderLab(ctx);

        await cut.InvokeAsync(() => cut.Find(".theme-lab-controls input[type='checkbox']").ChangeAsync(new ChangeEventArgs { Value = true }));

        JSRuntimeInvocation call = Assert.Single(module.Invocations["inspect"]);
        Assert.True(Assert.IsType<bool>(call.Arguments[0]));

        await ctx.DisposeComponentsOnDispatcherAsync();
        Assert.NotEmpty(module.Invocations["reset"]);
    }

    private MudBunitContext NewContext(string environment)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton<IWebHostEnvironment>(new FakeEnvironment(environment));
        ctx.Services.AddSingleton(this.appearance);
        return ctx;
    }

    private static BunitJSModuleInterop SetupModule(MudBunitContext ctx, List<string>? seen = null)
    {
        List<string> calls = seen ?? [];
        BunitJSModuleInterop module = ctx.JSInterop.SetupModule(ModulePath);
        foreach (string name in new[] { "apply", "reset", "copy", "inspect" })
        {
            // Complete each call: a planned invocation without SetVoidResult stays pending, and the lab's awaits then held
            // these tests for 20-30 s each.
            module.SetupVoid(name, _ =>
            {
                calls.Add(name);
                return true;
            }).SetVoidResult();
        }

        return module;
    }

    private static IRenderedComponent<ContainerFragment> RenderLab(MudBunitContext ctx) =>
        ctx.RenderWithPopovers(static builder =>
        {
            builder.OpenComponent<ThemeLab>(0);
            builder.CloseComponent();
        });

    /// <summary>A hosting environment with a chosen name and nothing else.</summary>
    /// <param name="environmentName">The environment name, such as <c>Development</c>.</param>
    private sealed class FakeEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Huddle.Tests";

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string WebRootPath { get; set; } = string.Empty;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
