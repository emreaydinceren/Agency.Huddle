using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>
/// <see cref="HelperRegistry"/>, <see cref="HelperSettingsStore"/> and <see cref="HelperSettingsResolver"/>: three layers
/// of configuration (code defaults, appsettings, the Human's row) resolved at each fire (Helpers Spec §6.6, plan HL-2, HL-7).
/// </summary>
public sealed class HelperSettingsTests
{
    private static readonly HelperDefaults Defaults = new(true, "claude", "haiku-x", "low", 50);

    /// <summary>A Helper stand-in that only carries an id and defaults.</summary>
    /// <param name="Id">The id.</param>
    /// <param name="Defaults">The code defaults.</param>
    private sealed record StubHelper(HelperId Id, HelperDefaults Defaults) : IHelperAgent
    {
        /// <inheritdoc/>
        public string DisplayName => this.Id.Value;

        /// <inheritdoc/>
        public string Description => "A stub.";

        /// <inheritdoc/>
        public IReadOnlyList<string> PromptKeys => [];
    }

    /// <summary>Builds a resolver over a temp data directory.</summary>
    /// <param name="dir">The temp data directory.</param>
    /// <param name="configure">Changes the options, or <see langword="null"/>.</param>
    private static (HelperSettingsResolver Resolver, HelperSettingsStore Store) Build(TempDataDir dir, Action<TeamOptions>? configure = null)
    {
        HelperSettingsStore store = new(dir.Options());
        return (new HelperSettingsResolver(store, dir.Options(configure), NullLogger<HelperSettingsResolver>.Instance), store);
    }

    /// <summary>Two Helpers with the same id are refused at startup, ignoring case.</summary>
    [Fact]
    public void Registry_DuplicateId_IsRefused()
    {
        StubHelper first = new(new HelperId("NeedsHuman"), Defaults);
        StubHelper second = new(new HelperId("needshuman"), Defaults);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new HelperRegistry([first, second]));

        Assert.Equal("Two Helpers are registered with the id 'needshuman'.", error.Message);
    }

    /// <summary>The registry lists Helpers in registration order and finds one by id.</summary>
    [Fact]
    public void Registry_KeepsOrderAndFindsById()
    {
        StubHelper first = new(new HelperId("A"), Defaults);
        StubHelper second = new(new HelperId("B"), Defaults);

        HelperRegistry registry = new([first, second]);

        Assert.Equal([first, second], registry.All);
        Assert.Same(second, registry.Find(new HelperId("b")));
        Assert.Null(registry.Find(new HelperId("C")));
    }

    /// <summary>With no row and no configuration, the code defaults apply.</summary>
    [Fact]
    public void For_NothingConfigured_UsesCodeDefaults()
    {
        using TempDataDir dir = new();

        HelperSettings settings = Build(dir).Resolver.For(new StubHelper(new HelperId("NeedsHuman"), Defaults));

        Assert.Equal(new HelperSettings(true, "claude", "haiku-x", "low", 50, 10, 10, 60), settings);
    }

    /// <summary>Configuration overrides the code defaults for the values it sets, and keeps the rest.</summary>
    [Fact]
    public void For_Configured_OverridesOnlyWhatItSets()
    {
        using TempDataDir dir = new();
        (HelperSettingsResolver resolver, _) = Build(dir, static options =>
            options.Helpers["needshuman"] = new HelperOptions { Enabled = false, DailyCap = 5, WaitSeconds = 3 });

        HelperSettings settings = resolver.For(new StubHelper(new HelperId("NeedsHuman"), Defaults));

        Assert.Equal(new HelperSettings(false, "claude", "haiku-x", "low", 5, 10, 3, 60), settings);
    }

    /// <summary>A row is all or nothing: its null Model is the Adapter's default, not the configured Model.</summary>
    [Fact]
    public void For_RowWithNullModel_MeansAdapterDefaultNotConfiguredModel()
    {
        using TempDataDir dir = new();
        (HelperSettingsResolver resolver, HelperSettingsStore store) = Build(dir, static options =>
            options.Helpers["NeedsHuman"] = new HelperOptions { Model = "configured-model" });
        HelperId id = new("NeedsHuman");
        store.Save(id, new HelperSettingsRow(true, "codex", null, null, 7));

        HelperSettings settings = resolver.For(new StubHelper(id, Defaults));

        Assert.Equal("codex", settings.Adapter);
        Assert.Null(settings.Model);
        Assert.Null(settings.Effort);
        Assert.Equal(7, settings.DailyCap);
        Assert.Equal(10, settings.ContinueGrant);
    }

    /// <summary>Reset to defaults deletes the row, so the configuration applies again.</summary>
    [Fact]
    public void Delete_RemovesTheRow()
    {
        using TempDataDir dir = new();
        (HelperSettingsResolver resolver, HelperSettingsStore store) = Build(dir);
        HelperId id = new("NeedsHuman");
        store.Save(id, new HelperSettingsRow(false, null, null, null, 3));

        store.Delete(id);

        Assert.Null(store.Get(id));
        Assert.Equal(50, resolver.For(new StubHelper(id, Defaults)).DailyCap);
    }

    /// <summary>A saved row replaces the earlier one and survives a new store instance.</summary>
    [Fact]
    public void Save_ReplacesAndSurvivesRestart()
    {
        using TempDataDir dir = new();
        HelperId id = new("NeedsHuman");
        HelperSettingsStore store = new(dir.Options());
        store.Save(id, new HelperSettingsRow(true, "claude", "m1", "low", 9));
        store.Save(id, new HelperSettingsRow(false, "claude", "m2", null, 4));

        HelperSettingsRow? row = new HelperSettingsStore(dir.Options()).Get(id);

        Assert.Equal(new HelperSettingsRow(false, "claude", "m2", null, 4), row);
    }

    /// <summary>A daily cap below 1, configured or saved, falls back to the code default.</summary>
    /// <param name="cap">The bad cap.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void For_CapBelowOne_FallsBackToDefault(int cap)
    {
        using TempDataDir dir = new();
        (HelperSettingsResolver configuredResolver, _) = Build(dir, options => options.Helpers["NeedsHuman"] = new HelperOptions { DailyCap = cap });
        Assert.Equal(50, configuredResolver.For(new StubHelper(new HelperId("NeedsHuman"), Defaults)).DailyCap);

        using TempDataDir other = new();
        (HelperSettingsResolver rowResolver, HelperSettingsStore store) = Build(other);
        store.Save(new HelperId("NeedsHuman"), new HelperSettingsRow(true, null, null, null, cap));

        Assert.Equal(50, rowResolver.For(new StubHelper(new HelperId("NeedsHuman"), Defaults)).DailyCap);
    }
}
