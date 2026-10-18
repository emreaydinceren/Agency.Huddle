using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Appearance;

namespace Agency.Huddle.Tests.Appearance;

/// <summary>
/// Tests for the per-Theme customizations in <c>appearance.json</c> (P5-D1) and the one-time migration of a file that
/// still names a retired Theme or carries <c>accentColor</c> (P5-D3): every entry point of the migration, every
/// validation refusal, and the writers.
/// </summary>
public sealed class AppearanceCustomizationTests
{
    /// <summary>Opens a store over a data directory holding <paramref name="json"/> as its <c>appearance.json</c>.</summary>
    /// <param name="dataDir">The data directory.</param>
    /// <param name="json">The file's text, or <see langword="null"/> for no file.</param>
    /// <param name="logger">The logger the store warns through.</param>
    private static AppearanceStore Open(TempDataDir dataDir, string? json, ILogger<AppearanceStore>? logger = null)
    {
        if (json is not null)
        {
            File.WriteAllText(Path.Combine(dataDir.Path, "appearance.json"), json);
        }

        return new AppearanceStore(dataDir.Options(), logger ?? NullLogger<AppearanceStore>.Instance);
    }

    /// <summary>The parsed <c>appearance.json</c> in <paramref name="dataDir"/>.</summary>
    /// <param name="dataDir">The data directory.</param>
    private static JsonObject ReadFile(TempDataDir dataDir) =>
        Assert.IsType<JsonObject>(JsonNode.Parse(File.ReadAllText(Path.Combine(dataDir.Path, "appearance.json"))));

    /// <summary>The <c>palette.primary</c> text stored for <paramref name="themeId"/> in the file, or <see langword="null"/>.</summary>
    /// <param name="file">The parsed file.</param>
    /// <param name="themeId">The Theme id.</param>
    private static string? StoredPrimary(JsonObject file, string themeId) =>
        file["customizations"]?[themeId]?["palette"]?["primary"]?.GetValue<string>();

    /// <summary>A <c>custom-accent</c> file with a colour becomes Lumen Light carrying that colour, and the file is rewritten without <c>accentColor</c>.</summary>
    [Fact]
    public void Migrate_CustomAccentLight_BecomesLumenLightWithTheAccentInItsCustomization()
    {
        using TempDataDir dataDir = new();

        using AppearanceStore store = Open(dataDir, """{"theme":"custom-accent","accentColor":"#00b294"}""");

        Assert.Equal("lumen-light", store.Current.ThemeId);
        Assert.Equal("#00b294", store.Current.CustomizationFor("lumen-light")?.Palette["primary"]);
        JsonObject file = ReadFile(dataDir);
        Assert.Equal("lumen-light", file["theme"]?.GetValue<string>());
        Assert.Equal("#00b294", StoredPrimary(file, "lumen-light"));
        Assert.False(file.ContainsKey("accentColor"));
    }

    /// <summary>A <c>custom-accent-dark</c> file becomes Lumen Dark carrying the colour.</summary>
    [Fact]
    public void Migrate_CustomAccentDark_BecomesLumenDarkWithTheAccentInItsCustomization()
    {
        using TempDataDir dataDir = new();

        using AppearanceStore store = Open(dataDir, """{"theme":"custom-accent-dark","accentColor":"#00b294"}""");

        Assert.Equal("lumen-dark", store.Current.ThemeId);
        Assert.Equal("#00b294", store.Current.CustomizationFor("lumen-dark")?.Palette["primary"]);
        Assert.Null(store.Current.CustomizationFor("lumen-light"));
        Assert.False(ReadFile(dataDir).ContainsKey("accentColor"));
    }

    /// <summary>A file that chose Custom Accent only as its light Theme, beside a different dark one, moves the colour to Lumen Light alone.</summary>
    [Fact]
    public void Migrate_CustomAccentOnlyAsTheLightChoice_CustomizesOnlyLumenLight()
    {
        using TempDataDir dataDir = new();

        using AppearanceStore store = Open(dataDir, """{"theme":"nordic-dark","lightTheme":"custom-accent","darkTheme":"nordic-dark","accentColor":"#00b294"}""");

        Assert.Equal("nordic-dark", store.Current.ThemeId);
        Assert.Equal("lumen-light", store.Current.LightThemeId);
        Assert.Equal("nordic-dark", store.Current.DarkThemeId);
        Assert.Equal("#00b294", store.Current.CustomizationFor("lumen-light")?.Palette["primary"]);
        Assert.Null(store.Current.CustomizationFor("lumen-dark"));
        Assert.Null(store.Current.CustomizationFor("nordic-dark"));
    }

    /// <summary>A file that chose both Custom Accent Themes moves the colour to both Lumen Themes.</summary>
    [Fact]
    public void Migrate_BothCustomAccentThemes_CustomizeBothLumenThemes()
    {
        using TempDataDir dataDir = new();

        using AppearanceStore store = Open(dataDir, """{"theme":"custom-accent-dark","lightTheme":"custom-accent","darkTheme":"custom-accent-dark","accentColor":"#00b294"}""");

        Assert.Equal("#00b294", store.Current.CustomizationFor("lumen-light")?.Palette["primary"]);
        Assert.Equal("#00b294", store.Current.CustomizationFor("lumen-dark")?.Palette["primary"]);
    }

    /// <summary>The former default <c>huddle</c> becomes Lumen Light, with no customization, and the file is rewritten once.</summary>
    [Fact]
    public void Migrate_Huddle_BecomesLumenLight()
    {
        using TempDataDir dataDir = new();

        using AppearanceStore store = Open(dataDir, """{"theme":"huddle"}""");

        Assert.Equal("lumen-light", store.Current.ThemeId);
        Assert.Empty(store.Current.Customizations);
        Assert.Equal("lumen-light", ReadFile(dataDir)["theme"]?.GetValue<string>());
    }

    /// <summary><c>huddle-dark</c> becomes Lumen Dark.</summary>
    [Fact]
    public void Migrate_HuddleDark_BecomesLumenDark()
    {
        using TempDataDir dataDir = new();

        using AppearanceStore store = Open(dataDir, """{"theme":"huddle-dark"}""");

        Assert.Equal("lumen-dark", store.Current.ThemeId);
        Assert.Equal("lumen-dark", ReadFile(dataDir)["theme"]?.GetValue<string>());
    }

    /// <summary>The light and dark choices are migrated too, not only the shown Theme.</summary>
    [Fact]
    public void Migrate_TheLightAndDarkChoices_AreMigratedToo()
    {
        using TempDataDir dataDir = new();

        using AppearanceStore store = Open(dataDir, """{"theme":"huddle","lightTheme":"huddle","darkTheme":"huddle-dark"}""");

        Assert.Equal("lumen-light", store.Current.LightThemeId);
        Assert.Equal("lumen-dark", store.Current.DarkThemeId);
        JsonObject file = ReadFile(dataDir);
        Assert.Equal("lumen-light", file["lightTheme"]?.GetValue<string>());
        Assert.Equal("lumen-dark", file["darkTheme"]?.GetValue<string>());
    }

    /// <summary>An unknown id is not migrated: it warns, falls back to the default Theme (now Lumen Light), and the file is left exactly as it was.</summary>
    [Fact]
    public void Migrate_AnUnknownId_FallsBackToLumenLightAndLeavesTheFileAlone()
    {
        using TempDataDir dataDir = new();
        WarningRecorder logger = new();

        using AppearanceStore store = Open(dataDir, """{"theme":"solarized","accentColor":"#00b294"}""", logger);

        Assert.Null(store.Current.ThemeId);
        Assert.Equal("lumen-light", store.Current.PreferredLightId);
        Assert.Equal("""{"theme":"solarized","accentColor":"#00b294"}""", File.ReadAllText(Path.Combine(dataDir.Path, "appearance.json")));
        Assert.Contains("solarized", Assert.Single(logger.Warnings), StringComparison.Ordinal);
    }

    /// <summary>A valid <c>accentColor</c> with no Custom Accent Theme chosen has nothing to move into, so it is left in the file untouched and customises nothing.</summary>
    [Fact]
    public void Migrate_AccentColorWithNoCustomAccentTheme_IsLeftAlone()
    {
        using TempDataDir dataDir = new();

        using AppearanceStore store = Open(dataDir, """{"theme":"nordic-light","accentColor":"#00b294"}""");

        Assert.Empty(store.Current.Customizations);
        Assert.Equal("""{"theme":"nordic-light","accentColor":"#00b294"}""", File.ReadAllText(Path.Combine(dataDir.Path, "appearance.json")));
    }

    /// <summary>A malformed <c>accentColor</c> beside a Custom Accent Theme warns and is kept; the Theme id still migrates.</summary>
    [Fact]
    public void Migrate_AMalformedAccentColor_WarnsAndIsKept()
    {
        using TempDataDir dataDir = new();
        WarningRecorder logger = new();

        using AppearanceStore store = Open(dataDir, """{"theme":"custom-accent","accentColor":"not-a-colour"}""", logger);

        Assert.Equal("lumen-light", store.Current.ThemeId);
        Assert.Empty(store.Current.Customizations);
        JsonObject file = ReadFile(dataDir);
        Assert.Equal("not-a-colour", file["accentColor"]?.GetValue<string>());
        Assert.Contains("not-a-colour", Assert.Single(logger.Warnings), StringComparison.Ordinal);
    }

    /// <summary>A customization already stored for Lumen Light is not overwritten by the legacy accent.</summary>
    [Fact]
    public void Migrate_AnExistingCustomization_IsNotOverwritten()
    {
        using TempDataDir dataDir = new();

        using AppearanceStore store = Open(dataDir, """
            {"theme":"custom-accent","accentColor":"#00b294",
             "customizations":{"lumen-light":{"palette":{"primary":"#112233"}}}}
            """);

        Assert.Equal("#112233", store.Current.CustomizationFor("lumen-light")?.Palette["primary"]);
        Assert.False(ReadFile(dataDir).ContainsKey("accentColor"));
    }

    /// <summary>The migrated file is a fixed point: opening it again changes nothing, so the rewrite happens once.</summary>
    [Fact]
    public void Migrate_IsWrittenOnce_AndASecondLoadChangesNothing()
    {
        using TempDataDir dataDir = new();
        using (AppearanceStore first = Open(dataDir, """{"theme":"custom-accent","accentColor":"#00b294","note":"keep"}"""))
        {
            Assert.Equal("lumen-light", first.Current.ThemeId);
        }

        string afterFirst = File.ReadAllText(Path.Combine(dataDir.Path, "appearance.json"));
        using AppearanceStore second = Open(dataDir, null);

        Assert.Equal(afterFirst, File.ReadAllText(Path.Combine(dataDir.Path, "appearance.json")));
        Assert.Equal("keep", ReadFile(dataDir)["note"]?.GetValue<string>());
        Assert.Equal("#00b294", second.Current.CustomizationFor("lumen-light")?.Palette["primary"]);
    }

    /// <summary>A file that needs no migration is not rewritten at all.</summary>
    [Fact]
    public void Migrate_NothingToMigrate_DoesNotRewriteTheFile()
    {
        using TempDataDir dataDir = new();
        const string Text = """{"theme":"nordic-dark"}""";

        using AppearanceStore store = Open(dataDir, Text);

        Assert.Equal(Text, File.ReadAllText(Path.Combine(dataDir.Path, "appearance.json")));
    }

    /// <summary>A malformed file is not migrated or rewritten: the tolerance for a bad file is unchanged.</summary>
    [Fact]
    public void Migrate_AMalformedFile_IsNotRewritten()
    {
        using TempDataDir dataDir = new();

        using AppearanceStore store = Open(dataDir, "{ not json");

        Assert.Null(store.Current.ThemeId);
        Assert.Equal("{ not json", File.ReadAllText(Path.Combine(dataDir.Path, "appearance.json")));
    }

    /// <summary>A customization naming a deleted Theme id warns and is ignored, while a valid one beside it still applies.</summary>
    [Fact]
    public void Current_ACustomizationForADeletedThemeId_WarnsAndIsIgnored()
    {
        using TempDataDir dataDir = new();
        WarningRecorder logger = new();

        using AppearanceStore store = Open(dataDir, """
            {"theme":"nordic-light","customizations":{
              "huddle":{"palette":{"primary":"#112233"}},
              "nordic-light":{"palette":{"primary":"#445566"}}}}
            """, logger);

        Assert.Null(store.Current.CustomizationFor("huddle"));
        Assert.Equal("#445566", store.Current.CustomizationFor("nordic-light")?.Palette["primary"]);
        Assert.Contains("huddle", Assert.Single(logger.Warnings), StringComparison.Ordinal);
        Assert.Contains("#112233", File.ReadAllText(Path.Combine(dataDir.Path, "appearance.json")), StringComparison.Ordinal);
    }

    /// <summary>Every shape of invalid customization is dropped with one warning naming it, never thrown, and the file is left as it was.</summary>
    /// <param name="customizations">The <c>customizations</c> value's JSON.</param>
    /// <param name="expectedWarningPart">Text the single warning must contain.</param>
    [Theory]
    [InlineData("""{"lumen-light":{"palette":{"nonsense":"#112233"}}}""", "palette.nonsense")]
    [InlineData("""{"lumen-light":{"palette":{"primary":"not-a-colour"}}}""", "palette.primary")]
    [InlineData("""{"lumen-light":{"palette":{"primary":42}}}""", "palette.primary")]
    [InlineData("""{"lumen-light":{"palette":{"primary":null}}}""", "palette.primary")]
    [InlineData("""{"lumen-light":{"tokens":{"no.such.token":"#112233"}}}""", "tokens.no.such.token")]
    [InlineData("""{"lumen-light":{"tokens":{"ground.rail":""}}}""", "tokens.ground.rail")]
    [InlineData("""{"lumen-light":{"tokens":{"ground.rail":"url(x)"}}}""", "tokens.ground.rail")]
    [InlineData("""{"lumen-light":{"tokens":{"ground.rail":"ref(no.such.token)"}}}""", "tokens.ground.rail")]
    [InlineData("""{"lumen-light":{"tokens":{"ground.rail":7}}}""", "tokens.ground.rail")]
    [InlineData("""{"lumen-light":{"palette":"red"}}""", "palette")]
    [InlineData("""{"lumen-light":{"tokens":["a"]}}""", "tokens")]
    [InlineData("""{"lumen-light":"red"}""", "lumen-light")]
    [InlineData("""{"lumen-light":{"extra":{}}}""", "extra")]
    [InlineData("""["lumen-light"]""", "customizations")]
    [InlineData("\"red\"", "customizations")]
    [InlineData("""{"Lumen-Light":{"palette":{"primary":"#112233"}}}""", "Lumen-Light")]
    [InlineData("""{"../etc":{"palette":{"primary":"#112233"}}}""", "../etc")]
    public void Current_AnInvalidCustomization_IsDroppedWithOneWarning(string customizations, string expectedWarningPart)
    {
        using TempDataDir dataDir = new();
        WarningRecorder logger = new();
        string text = $$"""{"theme":"lumen-light","customizations":{{customizations}}}""";

        using AppearanceStore store = Open(dataDir, text, logger);

        Assert.Empty(store.Current.Customizations);
        Assert.Contains(expectedWarningPart, Assert.Single(logger.Warnings), StringComparison.Ordinal);
        Assert.Equal(text, File.ReadAllText(Path.Combine(dataDir.Path, "appearance.json")));
    }

    /// <summary>A valid entry sitting beside an invalid one survives; only the invalid entry is dropped.</summary>
    [Fact]
    public void Current_ValidAndInvalidEntriesTogether_KeepsTheValidOnes()
    {
        using TempDataDir dataDir = new();
        WarningRecorder logger = new();

        using AppearanceStore store = Open(dataDir, """
            {"customizations":{"lumen-light":{
              "palette":{"primary":"#112233","bogus":"#ffffff"},
              "tokens":{"ground.rail":"#abcdef","bad.token":"#fff"}}}}
            """, logger);

        SkinCustomizationView view = new(store.Current.CustomizationFor("lumen-light"));
        Assert.Equal("#112233", view.Primary);
        Assert.Equal("#abcdef", view.Rail);
        Assert.Equal(2, logger.Warnings.Count);
    }

    /// <summary>Customizations survive a Theme change and a toggle: they belong to the Theme id, not to what is shown.</summary>
    [Fact]
    public void Customizations_SurviveSavingAndToggling()
    {
        using TempDataDir dataDir = new();
        using AppearanceStore store = Open(dataDir, null);
        store.SaveAccentColor("lumen-light", "#00b294");

        store.Save("nordic-light");
        store.ToggleMode();

        Assert.Equal("#00b294", store.Current.CustomizationFor("lumen-light")?.Palette["primary"]);
    }

    /// <summary><see cref="AppearanceStore.SaveAccentColor"/> writes <c>palette.primary</c> under the Theme id and leaves the selection alone.</summary>
    [Fact]
    public void SaveAccentColor_WritesPalettePrimaryUnderTheThemeId()
    {
        using TempDataDir dataDir = new();
        using AppearanceStore store = Open(dataDir, null);
        store.Save("nordic-light");

        store.SaveAccentColor("nordic-light", "#00b294");

        Assert.Equal("nordic-light", store.Current.ThemeId);
        Assert.Equal("#00b294", StoredPrimary(ReadFile(dataDir), "nordic-light"));
        Assert.Equal("#00b294", store.Current.CustomizationFor("nordic-light")?.Palette["primary"]);
        Assert.False(ReadFile(dataDir).ContainsKey("accentColor"));
    }

    /// <summary>Saving a colour keeps the other palette keys and the tokens already customised for that Theme.</summary>
    [Fact]
    public void SaveAccentColor_KeepsTheRestOfTheEntry()
    {
        using TempDataDir dataDir = new();
        using AppearanceStore store = Open(dataDir, """
            {"customizations":{"lumen-light":{"palette":{"drawerBackground":"#101010"},"tokens":{"ground.rail":"#abcdef"}}}}
            """);

        store.SaveAccentColor("lumen-light", "#00b294");

        SkinCustomizationView view = new(store.Current.CustomizationFor("lumen-light"));
        Assert.Equal("#00b294", view.Primary);
        Assert.Equal("#abcdef", view.Rail);
        Assert.Equal("#101010", store.Current.CustomizationFor("lumen-light")?.Palette["drawerBackground"]);
    }

    /// <summary>Clearing the colour removes <c>palette.primary</c>, and removes the whole entry when nothing else is customised.</summary>
    [Fact]
    public void SaveAccentColor_WithNull_ClearsTheColourAndPrunesAnEmptyEntry()
    {
        using TempDataDir dataDir = new();
        using AppearanceStore store = Open(dataDir, null);
        store.SaveAccentColor("lumen-light", "#00b294");

        store.SaveAccentColor("lumen-light", null);

        Assert.Null(store.Current.CustomizationFor("lumen-light"));
        Assert.DoesNotContain("lumen-light", File.ReadAllText(Path.Combine(dataDir.Path, "appearance.json")), StringComparison.Ordinal);
    }

    /// <summary>Clearing the colour keeps the entry's other customizations.</summary>
    [Fact]
    public void SaveAccentColor_WithNull_KeepsOtherCustomizations()
    {
        using TempDataDir dataDir = new();
        using AppearanceStore store = Open(dataDir, """
            {"customizations":{"lumen-light":{"palette":{"primary":"#112233"},"tokens":{"ground.rail":"#abcdef"}}}}
            """);

        store.SaveAccentColor("lumen-light", null);

        SkinCustomizationView view = new(store.Current.CustomizationFor("lumen-light"));
        Assert.Null(view.Primary);
        Assert.Equal("#abcdef", view.Rail);
    }

    /// <summary>A refused write changes nothing and raises nothing: a malformed colour, and an unknown or deleted Theme id.</summary>
    /// <param name="themeId">The Theme id passed.</param>
    /// <param name="hex">The colour passed.</param>
    [Theory]
    [InlineData("lumen-light", "not-a-colour")]
    [InlineData("lumen-light", "#12345")]
    [InlineData("lumen-light", "#1234567")]
    [InlineData("lumen-light", "rgb(1,2,3)")]
    [InlineData("lumen-light", "#00b294; x")]
    [InlineData("lumen-light", "")]
    [InlineData("huddle", "#00b294")]
    [InlineData("custom-accent", "#00b294")]
    [InlineData("nonsense", "#00b294")]
    [InlineData("../x", "#00b294")]
    public void SaveAccentColor_ARefusedValue_ChangesNothingAndRaisesNothing(string themeId, string hex)
    {
        using TempDataDir dataDir = new();
        using AppearanceStore store = Open(dataDir, """{"theme":"lumen-light"}""");
        bool raised = false;
        store.AppearanceChanged += () => raised = true;

        store.SaveAccentColor(themeId, hex);

        Assert.False(raised);
        Assert.Empty(store.Current.Customizations);
        Assert.Equal("""{"theme":"lumen-light"}""", File.ReadAllText(Path.Combine(dataDir.Path, "appearance.json")));
    }

    /// <summary>Saving a colour raises <see cref="AppearanceStore.AppearanceChanged"/> after the write and the rebuild.</summary>
    [Fact]
    public void SaveAccentColor_RaisesAppearanceChangedWithTheNewSnapshot()
    {
        using TempDataDir dataDir = new();
        using AppearanceStore store = Open(dataDir, null);
        string? seen = null;
        store.AppearanceChanged += () => seen = store.Current.CustomizationFor("lumen-light")?.Palette["primary"];

        store.SaveAccentColor("lumen-light", "#00b294");

        Assert.Equal("#00b294", seen);
    }

    /// <summary>Reset removes the Theme's whole entry, palette and tokens, and nothing of another Theme's.</summary>
    [Fact]
    public void ResetCustomization_RemovesTheWholeEntryOfThatThemeOnly()
    {
        using TempDataDir dataDir = new();
        using AppearanceStore store = Open(dataDir, """
            {"customizations":{
              "lumen-light":{"palette":{"primary":"#112233"},"tokens":{"ground.rail":"#abcdef"}},
              "nordic-light":{"palette":{"primary":"#445566"}}}}
            """);

        store.ResetCustomization("lumen-light");

        Assert.Null(store.Current.CustomizationFor("lumen-light"));
        Assert.Equal("#445566", store.Current.CustomizationFor("nordic-light")?.Palette["primary"]);
        JsonObject file = ReadFile(dataDir);
        Assert.Null(file["customizations"]?["lumen-light"]);
        Assert.Equal("#445566", StoredPrimary(file, "nordic-light"));
    }

    /// <summary>Resetting the last entry removes the <c>customizations</c> key, and unknown keys beside it stay.</summary>
    [Fact]
    public void ResetCustomization_TheLastEntry_RemovesTheKeyAndKeepsOtherKeys()
    {
        using TempDataDir dataDir = new();
        using AppearanceStore store = Open(dataDir, """{"note":"keep","customizations":{"lumen-light":{"palette":{"primary":"#112233"}}}}""");

        store.ResetCustomization("lumen-light");

        JsonObject file = ReadFile(dataDir);
        Assert.False(file.ContainsKey("customizations"));
        Assert.Equal("keep", file["note"]?.GetValue<string>());
    }

    /// <summary>Resetting a Theme that has no entry writes nothing and raises nothing.</summary>
    [Fact]
    public void ResetCustomization_WithNoEntry_DoesNothing()
    {
        using TempDataDir dataDir = new();
        using AppearanceStore store = Open(dataDir, null);
        bool raised = false;
        store.AppearanceChanged += () => raised = true;

        store.ResetCustomization("lumen-light");

        Assert.False(raised);
        Assert.False(File.Exists(Path.Combine(dataDir.Path, "appearance.json")));
    }

    /// <summary>Resetting raises <see cref="AppearanceStore.AppearanceChanged"/> when it removed an entry.</summary>
    [Fact]
    public void ResetCustomization_RaisesAppearanceChanged()
    {
        using TempDataDir dataDir = new();
        using AppearanceStore store = Open(dataDir, null);
        store.SaveAccentColor("lumen-light", "#00b294");
        bool raised = false;
        store.AppearanceChanged += () => raised = true;

        store.ResetCustomization("lumen-light");

        Assert.True(raised);
    }

    /// <summary>A customization edited by hand while the app runs is picked up by the watcher.</summary>
    [Fact]
    public async Task Current_AHandEditedCustomization_IsPickedUpByTheWatcher()
    {
        using TempDataDir dataDir = new();
        using AppearanceStore store = Open(dataDir, null);

        File.WriteAllText(Path.Combine(dataDir.Path, "appearance.json"), """{"customizations":{"lumen-light":{"palette":{"primary":"#112233"}}}}""");

        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (store.Current.CustomizationFor("lumen-light") is null)
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for the filesystem watcher.");
            await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        }

        Assert.Equal("#112233", store.Current.CustomizationFor("lumen-light")?.Palette["primary"]);
    }

    /// <summary>Reads the two entries these tests care about out of a customization.</summary>
    private sealed class SkinCustomizationView
    {
        /// <summary>Wraps <paramref name="customization"/>.</summary>
        /// <param name="customization">The customization, or <see langword="null"/>.</param>
        public SkinCustomizationView(Agency.Huddle.App.Themes.Skins.SkinCustomization? customization)
        {
            this.Primary = customization is not null && customization.Palette.TryGetValue("primary", out string? primary) ? primary : null;
            this.Rail = customization is not null && customization.Tokens.TryGetValue("ground.rail", out string? rail) ? rail : null;
        }

        /// <summary>The <c>palette.primary</c> text, or <see langword="null"/>.</summary>
        public string? Primary { get; }

        /// <summary>The <c>tokens["ground.rail"]</c> text, or <see langword="null"/>.</summary>
        public string? Rail { get; }
    }

    /// <summary>Collects the formatted text of every warning an <see cref="AppearanceStore"/> logs.</summary>
    private sealed class WarningRecorder : ILogger<AppearanceStore>
    {
        /// <summary>The formatted warning messages, in order.</summary>
        public List<string> Warnings { get; } = [];

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                this.Warnings.Add(formatter(state, exception));
            }
        }
    }
}
