using System.Text.Json;
using Microsoft.Extensions.Logging;
using Agency.Huddle.App.Acp;
using Agency.Huddle.Tests.Elicitation;
using Agency.Huddle.Tests.Waiting;
using Store = Agency.Huddle.App.Acp.Sessions.SessionSettingsStore;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// Tests for the session-settings store: the overrides-only, watched, hand-editable
/// <c>{DataDir}/session-settings.json</c> that holds the catch-up message cap and token budget
/// (Spec 5.2, LR-R9, LR-R10, LR-E12).
/// </summary>
public sealed class SessionSettingsStoreTests
{
    private const string MessagesKey = "transcriptCatchUpMessages";
    private const string TokensKey = "transcriptCatchUpTokens";

    /// <summary>With no file the store reports the configured values (deliberately not the <c>AcpOptions</c> defaults) and creates no file.</summary>
    [Fact]
    public void Current_WithNoFile_EqualsTheConfiguredValuesAndCreatesNothing()
    {
        using var dataDir = new TempDataDir();
        using Store store = Create(dataDir);
        Agency.Huddle.App.Acp.Sessions.ISessionSettings settings = Assert.IsAssignableFrom<Agency.Huddle.App.Acp.Sessions.ISessionSettings>(store);

        Assert.Equal(30, settings.Current.CatchUpMessages);
        Assert.Equal(50_000, settings.Current.CatchUpTokens);
        Assert.Null(store.LoadError);
        Assert.Null(store.MessagesOverride);
        Assert.Null(store.TokensOverride);
        Assert.False(File.Exists(store.FilePath));
        Assert.Equal(Path.Combine(dataDir.Path, "session-settings.json"), store.FilePath);
    }

    /// <summary>A valid save writes only the overridden key, and the store reports it.</summary>
    [Fact]
    public void SaveMessages_WithAValidValue_WritesOnlyThatKey()
    {
        using var dataDir = new TempDataDir();
        using Store store = Create(dataDir);

        string? refusal = store.SaveMessages(400);

        Assert.Null(refusal);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(store.FilePath));
        Assert.Equal([MessagesKey], document.RootElement.EnumerateObject().Select(static property => property.Name).ToArray());
        Assert.Equal(400, document.RootElement.GetProperty(MessagesKey).GetInt32());
        Assert.Equal(400, store.MessagesOverride);
        Assert.Equal(400, store.Current.CatchUpMessages);
        Assert.Equal(50_000, store.Current.CatchUpTokens);
    }

    /// <summary>A refused cap returns the T6 text, leaves the file byte-identical and raises no <c>Changed</c>.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(2001)]
    public void SaveMessages_OutOfRange_ReturnsTheRefusalAndLeavesTheFileAlone(int value)
    {
        using var dataDir = new TempDataDir();
        string path = Path.Combine(dataDir.Path, "session-settings.json");
        File.WriteAllText(path, "{\"" + TokensKey + "\":40000}");
        byte[] before = File.ReadAllBytes(path);
        using Store store = Create(dataDir);
        int changed = 0;
        store.Changed += () => Interlocked.Increment(ref changed);

        string? refusal = store.SaveMessages(value);

        Assert.Equal("Enter a number from 1 to 2,000.", refusal);
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal(0, Volatile.Read(ref changed));
    }

    /// <summary>A refused budget returns the T7 text and leaves the file byte-identical.</summary>
    [Theory]
    [InlineData(999)]
    [InlineData(150_001)]
    public void SaveTokens_OutOfRange_ReturnsTheRefusalAndLeavesTheFileAlone(int value)
    {
        using var dataDir = new TempDataDir();
        string path = Path.Combine(dataDir.Path, "session-settings.json");
        File.WriteAllText(path, "{\"" + MessagesKey + "\":400}");
        byte[] before = File.ReadAllBytes(path);
        using Store store = Create(dataDir);

        string? refusal = store.SaveTokens(value);

        Assert.Equal("Enter a number from 1,000 to 150,000.", refusal);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    /// <summary>Resetting one value removes only its key; the other override stays.</summary>
    [Fact]
    public void SaveTokens_Null_RemovesOnlyThatKey()
    {
        using var dataDir = new TempDataDir();
        File.WriteAllText(Path.Combine(dataDir.Path, "session-settings.json"), "{\"" + MessagesKey + "\":400,\"" + TokensKey + "\":40000}");
        using Store store = Create(dataDir);

        string? refusal = store.SaveTokens(null);

        Assert.Null(refusal);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(store.FilePath));
        Assert.Equal([MessagesKey], document.RootElement.EnumerateObject().Select(static property => property.Name).ToArray());
        Assert.Null(store.TokensOverride);
        Assert.Equal(50_000, store.Current.CatchUpTokens);
        Assert.Equal(400, store.Current.CatchUpMessages);
    }

    /// <summary>Removing the last override deletes the file rather than leaving an empty object.</summary>
    [Fact]
    public void SaveMessages_Null_RemovingTheLastKeyDeletesTheFile()
    {
        using var dataDir = new TempDataDir();
        File.WriteAllText(Path.Combine(dataDir.Path, "session-settings.json"), "{\"" + MessagesKey + "\":400}");
        using Store store = Create(dataDir);

        string? refusal = store.SaveMessages(null);

        Assert.Null(refusal);
        Assert.False(File.Exists(store.FilePath));
        Assert.Equal(30, store.Current.CatchUpMessages);
    }

#pragma warning disable S2699 // The test's only check besides Changed is the Eventually.UntilAsync wait, which fails the test when the condition never holds; Sonar cannot see an assertion inside the shared helper.
    /// <summary>A hand edit made outside the process is picked up by the watcher once its debounce settles: <c>Current</c> updates and <c>Changed</c> is raised.</summary>
    [Fact]
    public async Task Current_ExternalEdit_IsPickedUpByTheWatcherAndRaisesChanged()
    {
        using var dataDir = new TempDataDir();
        using Store store = Create(dataDir);
        int changed = 0;
        store.Changed += () => Interlocked.Increment(ref changed);

        File.WriteAllText(store.FilePath, "{\"" + MessagesKey + "\":123}");

        await Eventually.UntilAsync(() => store.Current.CatchUpMessages == 123, TestContext.Current.CancellationToken, timeout: TestBudgets.Slow, interval: TimeSpan.FromMilliseconds(50));
        Assert.True(Volatile.Read(ref changed) >= 1);
    }
#pragma warning restore S2699

    /// <summary>A corrupt file falls back to the configured values, sets <c>LoadError</c> and logs exactly one Warning.</summary>
    [Fact]
    public void Current_WithACorruptFile_FallsBackToTheDefaultsAndWarnsOnce()
    {
        using var dataDir = new TempDataDir();
        File.WriteAllText(Path.Combine(dataDir.Path, "session-settings.json"), "{ this is not valid json");
        RecordingLogger<Store> logger = new();

        using Store store = Create(dataDir, logger);

        Assert.Equal(30, store.Current.CatchUpMessages);
        Assert.Equal(50_000, store.Current.CatchUpTokens);
        Assert.False(string.IsNullOrWhiteSpace(store.LoadError));
        Assert.Single(logger.AtExactly(LogLevel.Warning));
    }

    /// <summary>A hand-edited out-of-range value is ignored for that key only, and <c>LoadError</c> names the key.</summary>
    [Fact]
    public void Current_WithAnOutOfRangeValue_IgnoresThatKeyAndNamesItInLoadError()
    {
        using var dataDir = new TempDataDir();
        File.WriteAllText(Path.Combine(dataDir.Path, "session-settings.json"), "{\"" + MessagesKey + "\":5000,\"" + TokensKey + "\":40000}");

        using Store store = Create(dataDir);

        Assert.Equal(30,store.Current.CatchUpMessages);
        Assert.Null(store.MessagesOverride);
        Assert.Equal(40_000, store.Current.CatchUpTokens);
        Assert.Equal(40_000, store.TokensOverride);
        Assert.NotNull(store.LoadError);
        Assert.Contains(MessagesKey, store.LoadError, StringComparison.Ordinal);
    }

    /// <summary>
    /// Guard row (green on arrival, LR-R10): the catch-up settings are global, so no
    /// <see cref="Persona"/> member is named like <c>CatchUp*</c>.
    /// </summary>
    [Fact]
    public void Persona_HasNoCatchUpMember()
    {
        string[] names = [.. typeof(Persona).GetProperties().Select(static property => property.Name)];

        Assert.DoesNotContain(names, static name => name.StartsWith("CatchUp", StringComparison.Ordinal));
    }

    /// <summary>Builds a store over the data directory, configured with non-default catch-up values.</summary>
    private static Store Create(TempDataDir dataDir, ILogger<Store>? logger = null) =>
        new(
            dataDir.Options(static o =>
            {
                o.Acp.TranscriptCatchUpMessages = 30;
                o.Acp.TranscriptCatchUpTokens = 50_000;
            }),
            logger ?? new RecordingLogger<Store>());
}
