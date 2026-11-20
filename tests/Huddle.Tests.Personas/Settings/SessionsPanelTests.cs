using System.Text.Json;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Components.Settings;
using Agency.Huddle.Tests.Ui;
using Agency.Huddle.Tests.Waiting;
using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;

namespace Agency.Huddle.Tests.Settings;

/// <summary>
/// <see cref="SessionsPanel"/> (Spec 5.4, LR-R13, LR-E12; texts T3 to T9) over a real <see cref="SessionSettingsStore"/> on a temp Data Dir.
/// The panel holds two <c>MudNumericField&lt;int&gt;</c>: the Message cap first, the token budget second. A test reads a field's label
/// (<c>label</c>), its helper or error text (<c>.mud-input-helper-text</c>) and its <c>input</c> through the component, so it needs no
/// class hook of its own. A <c>Reset</c> is a button whose trimmed text is exactly <c>Reset</c>. A save is committed by a change then a blur.
/// </summary>
public sealed class SessionsPanelTests : IAsyncDisposable
{
    private const string MessagesKey = "transcriptCatchUpMessages";
    private const string TokensKey = "transcriptCatchUpTokens";

    private readonly MudBunitContext ctx = new();
    private readonly TempDataDir dir = new();
    private SessionSettingsStore? store;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await this.ctx.DisposeAsync();
        this.store?.Dispose();
        this.dir.Dispose();
    }

    private SessionSettingsStore Store => this.store ?? this.StartStore();

    private SessionSettingsStore StartStore()
    {
        this.store = new SessionSettingsStore(this.dir.Options(), NullLogger<SessionSettingsStore>.Instance);
        this.ctx.Services.AddSingleton(this.store);
        return this.store;
    }

    private IRenderedComponent<ContainerFragment> Render()
    {
        _ = this.Store;
        return this.ctx.RenderWithPopovers<SessionsPanel>();
    }

    /// <summary>The panel draws no heading of its own: the Settings page's tab title already says "Sessions", and a second one repeated its parent (#473).</summary>
    [Fact]
    public void Panel_DrawsNoHeadingOfItsOwn()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.Empty(cut.FindAll("h1, h2, h3, h4, h5, h6"));
    }

    private static IRenderedComponent<MudNumericField<int>> Cap(IRenderedComponent<ContainerFragment> cut) =>
        cut.FindComponents<MudNumericField<int>>()[0];

    private static IRenderedComponent<MudNumericField<int>> Budget(IRenderedComponent<ContainerFragment> cut) =>
        cut.FindComponents<MudNumericField<int>>()[1];

    private static string Label(IRenderedComponent<MudNumericField<int>> field) => field.Find("label").TextContent.Trim();

    private static string Helper(IRenderedComponent<MudNumericField<int>> field) => field.Find(".mud-input-helper-text").TextContent.Trim();

    private static string? Shown(IRenderedComponent<MudNumericField<int>> field) => field.Find("input").GetAttribute("value");

    private static List<IElement> ResetButtons(IRenderedComponent<ContainerFragment> cut) =>
        cut.FindAll("button").Where(static button => string.Equals(button.TextContent.Trim(), "Reset", StringComparison.Ordinal)).ToList();

    /// <summary>Types <paramref name="text"/> into the field and leaves it, which is how a user commits a value.</summary>
    private static async Task EnterAsync(IRenderedComponent<ContainerFragment> cut, IRenderedComponent<MudNumericField<int>> field, string text)
    {
        IElement input = field.Find("input");
        await cut.InvokeAsync(() => input.ChangeAsync(new ChangeEventArgs { Value = text }));
        await cut.InvokeAsync(() => field.Find("input").BlurAsync());
    }

    /// <summary>Waits for <paramref name="condition"/>, bounded so a missing panel fails instead of hanging; a throwing condition counts as not yet true.</summary>
    private static Task UntilAsync(Func<bool> condition) =>
        Eventually.UntilAsync(() => { try { return condition(); } catch (ArgumentOutOfRangeException) { return false; } }, Xunit.TestContext.Current.CancellationToken, TimeSpan.FromSeconds(10));

    private static bool FileHasKey(string path, string key)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.TryGetProperty(key, out _);
    }

    private static int FileValue(string path, string key)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty(key).GetInt32();
    }

    /// <summary>The cap field's label is T3 and the budget field's label is T4.</summary>
    [Fact]
    public void Fields_ShowTheSettledLabels()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.Empty(cut.FindAll("[role=alert]"));
        Assert.Equal("Messages a fresh session rereads", Label(Cap(cut)));
        Assert.Equal("Size of what a fresh session rereads", Label(Budget(cut)));
    }

    /// <summary>The budget field shows its value with the same thousands separator as the hint under it ("100,000"), not "100000" (#479).</summary>
    [Fact]
    public void BudgetField_ShowsTheValueWithAThousandsSeparator()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.Equal("100,000", cut.Find(".sessions-panel-budget-row input").GetAttribute("value"));
    }

    /// <summary>With no override the budget helper is T5over the default 100,000 formatted with a thousands separator.</summary>
    [Fact]
    public void BudgetHelper_ShowsTheCurrentDefaultWithThousandsSeparator()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render();

        Assert.Equal("About 100,000 tokens (an estimate).", Helper(Budget(cut)));
    }

    /// <summary>A saved budget shows in the helper text as <c>N0</c> (T5).</summary>
    [Fact]
    public async Task BudgetHelper_FollowsASavedBudget()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render();

        await cut.InvokeAsync(() => this.Store.SaveTokens(5000));

        await UntilAsync(() => Helper(Budget(cut)) == "About 5,000 tokens (an estimate).");
        Assert.Equal("About 5,000 tokens (an estimate).", Helper(Budget(cut)));
    }

    /// <summary>Entering 0 in the cap shows T6 on that field and writes nothing.</summary>
    [Fact]
    public async Task Cap_EnteringZero_ShowsRefusalAndWritesNothing()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render();

        await EnterAsync(cut, Cap(cut), "0");

        Assert.Equal("Enter a number from 1 to 2,000.", Helper(Cap(cut)));
        Assert.False(File.Exists(this.Store.FilePath));
        Assert.Null(this.Store.MessagesOverride);
    }

    /// <summary>Entering 999 in the budget shows T7 on that field and writes nothing.</summary>
    [Fact]
    public async Task Budget_EnteringTooSmall_ShowsRefusalAndWritesNothing()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render();

        await EnterAsync(cut, Budget(cut), "999");

        Assert.Equal("Enter a number from 1,000 to 150,000.", Helper(Budget(cut)));
        Assert.False(File.Exists(this.Store.FilePath));
        Assert.Null(this.Store.TokensOverride);
    }

    /// <summary>A save on blur says so (a Snackbar, as Skills does), and a refused value says nothing of the kind (#737).</summary>
    [Fact]
    public async Task SaveOnBlur_ConfirmsInASnackbar_ButARefusalDoesNot()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render();
        ISnackbar snackbar = this.ctx.Services.GetRequiredService<ISnackbar>();

        await EnterAsync(cut, Cap(cut), "0");
        Assert.Empty(snackbar.ShownSnackbars);

        await EnterAsync(cut, Cap(cut), "400");
        Assert.Equal("Saved.", Assert.Single(snackbar.ShownSnackbars).Message?.ToString());
    }

    /// <summary>Entering 400 in the cap saves it: the file holds 400 and the field shows it.</summary>
    [Fact]
    public async Task Cap_EnteringFourHundred_SavesAndShowsIt()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render();

        await EnterAsync(cut, Cap(cut), "400");

        Assert.Equal(400, FileValue(this.Store.FilePath, MessagesKey));
        Assert.Equal(400, this.Store.MessagesOverride);
        Assert.Equal("400", Shown(Cap(cut)));
    }

    /// <summary>The cap's Reset (T8) is absent without an override; with one it removes the override and shows the default again.</summary>
    [Fact]
    public async Task CapReset_RemovesTheOverrideAndShowsTheDefault()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render();
        Assert.Empty(ResetButtons(cut));
        string? defaultShown = Shown(Cap(cut));

        await cut.InvokeAsync(() => this.Store.SaveMessages(400));
        await UntilAsync(() => ResetButtons(cut).Count == 1);
        Assert.Equal("400", Shown(Cap(cut)));
        await cut.InvokeAsync(() => ResetButtons(cut)[0].ClickAsync());

        Assert.Null(this.Store.MessagesOverride);
        Assert.False(FileHasKey(this.Store.FilePath, MessagesKey));
        Assert.Equal(defaultShown, Shown(Cap(cut)));
        Assert.Equal("20", Shown(Cap(cut)));
        await UntilAsync(() => ResetButtons(cut).Count == 0);
        Assert.Empty(ResetButtons(cut));
    }

    /// <summary>The budget's Reset (T8) removes the override and the helper shows the default again.</summary>
    [Fact]
    public async Task BudgetReset_RemovesTheOverrideAndShowsTheDefault()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render();

        await cut.InvokeAsync(() => this.Store.SaveTokens(5000));
        await UntilAsync(() => ResetButtons(cut).Count == 1);
        await cut.InvokeAsync(() => ResetButtons(cut)[0].ClickAsync());

        Assert.Null(this.Store.TokensOverride);
        Assert.False(FileHasKey(this.Store.FilePath, TokensKey));
        Assert.Equal("About 100,000 tokens (an estimate).", Helper(Budget(cut)));
        await UntilAsync(() => ResetButtons(cut).Count == 0);
        Assert.Empty(ResetButtons(cut));
    }

    /// <summary>
    /// A corrupt file shows T9 with the store's error, in a Warning notice with <c>role="status"</c>: the defaults apply, so it
    /// reports a state the Human can fix later rather than interrupting (Q-A1-1).
    /// </summary>
    [Fact]
    public void CorruptFile_ShowsTheErrorInAnAlert()
    {
        File.WriteAllText(Path.Combine(this.dir.Path, "session-settings.json"), "{ this is not json");

        IRenderedComponent<ContainerFragment> cut = this.Render();

        string? error = this.Store.LoadError;
        Assert.NotNull(error);
        Assert.Equal("session-settings.json could not be read, so the defaults apply: " + error, cut.Find(".sessions-panel-load-error[role=status]").TextContent.Trim());
    }

    /// <summary>A hand edit of the file re-renders the panel with the new value.</summary>
    [Fact]
    public async Task HandEdit_ReRendersThePanel()
    {
        IRenderedComponent<ContainerFragment> cut = this.Render();
        Assert.Equal("20", Shown(Cap(cut)));

        File.WriteAllText(this.Store.FilePath, "{ \"" + MessagesKey + "\": 77 }");

        await UntilAsync(() => Shown(Cap(cut)) == "77");
        Assert.Equal("77", Shown(Cap(cut)));
    }
}
