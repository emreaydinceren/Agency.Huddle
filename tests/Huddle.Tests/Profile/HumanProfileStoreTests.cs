using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Profile;

namespace Agency.Huddle.Tests.Profile;

/// <summary>
/// Tests for <see cref="HumanProfileStore"/>: a missing <c>profile.json</c> is the first-run state, a save
/// round-trips and raises <see cref="HumanProfileStore.HumanRenamed"/> before
/// <see cref="HumanProfileStore.ProfileChanged"/>, and a bad hand edit never loses the Human's Name.
/// </summary>
public sealed class HumanProfileStoreTests
{
    /// <summary>With no file the Human is unsaved, named by <c>Team:HumanName</c>, and nothing is written.</summary>
    [Fact]
    public void Current_WithNoFile_IsUnsavedConfiguredNameAndCreatesNothing()
    {
        using TempDataDir dataDir = new();
        using HumanProfileStore store = NewStore(dataDir);

        Assert.False(store.IsSaved);
        Assert.Equal("You", store.Name);
        Assert.False(File.Exists(store.FilePath));
    }

    /// <summary>A save writes every field, trims them, stores a blank optional field as absent, and marks the profile saved.</summary>
    [Fact]
    public void Save_RoundTripsTrimmedFields_AndMarksSaved()
    {
        using TempDataDir dataDir = new();
        using (HumanProfileStore store = NewStore(dataDir))
        {
            store.Save(new HumanProfile(" Alan ", "al", " Site Reliability Engineer ", "  "));
        }

        using HumanProfileStore reloaded = NewStore(dataDir);

        Assert.True(reloaded.IsSaved);
        Assert.Equal(new HumanProfile("Alan", "al", "Site Reliability Engineer", null), reloaded.Current);
    }

    /// <summary>A rename raises <see cref="HumanProfileStore.HumanRenamed"/> with both Names, and it comes before <see cref="HumanProfileStore.ProfileChanged"/>.</summary>
    [Fact]
    public void Save_NewName_RaisesRenamedBeforeChanged()
    {
        using TempDataDir dataDir = new();
        using HumanProfileStore store = NewStore(dataDir);
        List<string> events = [];
        store.HumanRenamed += renamed => events.Add($"renamed {renamed.OldName}->{renamed.NewName}");
        store.ProfileChanged += () => events.Add("changed");

        store.Save(new HumanProfile("Alan", null, null, null));

        Assert.Equal(["renamed You->Alan", "changed"], events);
    }

    /// <summary>A case-only rename still counts as a rename, so the cascade fixes the casing everywhere.</summary>
    [Fact]
    public void Save_CaseOnlyRename_RaisesRenamed()
    {
        using TempDataDir dataDir = new();
        using HumanProfileStore store = NewStore(dataDir);
        HumanRenamed? seen = null;
        store.HumanRenamed += renamed => seen = renamed;

        store.Save(new HumanProfile("you", null, null, null));

        Assert.Equal(new HumanRenamed("You", "you"), seen);
    }

    /// <summary>Changing only the Title raises <see cref="HumanProfileStore.ProfileChanged"/> but no rename.</summary>
    [Fact]
    public void Save_TitleOnly_RaisesChangedWithoutRename()
    {
        using TempDataDir dataDir = new();
        using HumanProfileStore store = NewStore(dataDir);
        int renames = 0;
        int changes = 0;
        store.HumanRenamed += _ => renames++;
        store.ProfileChanged += () => changes++;

        store.Save(new HumanProfile("You", null, "Engineer", null));

        Assert.Equal(0, renames);
        Assert.Equal(1, changes);
    }

    /// <summary><see cref="IHumanIdentity.Matches"/> accepts the Name or the Alias in any case, and nothing else.</summary>
    [Fact]
    public void Matches_NameOrAliasIgnoringCase()
    {
        using TempDataDir dataDir = new();
        using HumanProfileStore store = NewStore(dataDir);
        store.Save(new HumanProfile("Alan", "al", null, null));

        Assert.True(store.Matches("ALAN"));
        Assert.True(store.Matches("Al"));
        Assert.False(store.Matches("You"));
        Assert.False(store.Matches(null));
    }

    /// <summary>A hand-edited file with an invalid Name keeps the previous Name rather than losing the Human.</summary>
    [Fact]
    public void Current_InvalidNameInFile_KeepsTheConfiguredName()
    {
        using TempDataDir dataDir = new();
        File.WriteAllText(Path.Combine(dataDir.Path, "profile.json"), "{ \"name\": \"../evil\", \"title\": \"Boss\" }");

        using HumanProfileStore store = NewStore(dataDir);

        Assert.Equal("You", store.Name);
        Assert.Equal("Boss", store.Current.Title);
    }

    /// <summary>An edit made outside the app is picked up by the watcher and raises a rename.</summary>
    [Fact]
    public async Task Current_ExternalRename_IsPickedUpAndRaisesRenamed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        using HumanProfileStore store = NewStore(dataDir);
        TaskCompletionSource<HumanRenamed> renamed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        store.HumanRenamed += value => renamed.TrySetResult(value);

        await File.WriteAllTextAsync(store.FilePath, "{ \"name\": \"Alan\" }", ct);

        HumanRenamed seen = await renamed.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        Assert.Equal(new HumanRenamed("You", "Alan"), seen);
        Assert.True(store.IsSaved);
    }

    /// <summary>The file keeps a hand-added key across a save, and stays readable: accents are written as themselves, not escaped.</summary>
    [Fact]
    public void Save_KeepsUnknownKeys_AndWritesReadableJson()
    {
        using TempDataDir dataDir = new();
        File.WriteAllText(Path.Combine(dataDir.Path, "profile.json"), "{ \"name\": \"You\", \"note\": \"mine\" }");
        using HumanProfileStore store = NewStore(dataDir);

        store.Save(new HumanProfile("You", null, "Ingénieur \"SRE\"", null));

        string json = File.ReadAllText(store.FilePath);
        // contains-ok: the whole file's layout is not under test, only that the key survives and stays unescaped.
        Assert.Contains("\"note\": \"mine\"", json, StringComparison.Ordinal);
        // contains-ok: the accent is written as itself, not as a \\u escape.
        Assert.Contains("Ingénieur \\\"SRE\\\"", json, StringComparison.Ordinal);
    }

    /// <summary>A store over <paramref name="dataDir"/> with the stock first-run Name, <c>You</c>.</summary>
    /// <param name="dataDir">The data directory.</param>
    private static HumanProfileStore NewStore(TempDataDir dataDir) => new(dataDir.Options(), NullLogger<HumanProfileStore>.Instance);
}
