using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Pins <see cref="PlaybookBaseStore"/> (Team Playbooks spec §5.2): the old default's text, kept under <c>{DataDir}/playbook-defaults</c> and never edited.</summary>
public sealed class PlaybookBaseStoreTests : IDisposable
{
    private readonly string dataDir = Path.Combine(Path.GetTempPath(), "huddle-playbook-bases-" + Guid.NewGuid().ToString("N"));

    /// <summary>Removes the data folder.</summary>
    public void Dispose()
    {
        if (Directory.Exists(this.dataDir))
        {
            Directory.Delete(this.dataDir, recursive: true);
        }
    }

    /// <summary>The hash is 16 lower-case hex characters and ignores line endings and surrounding blank space.</summary>
    [Fact]
    public void Hash_IsSixteenHexCharacters_OfTheLfNormalisedTrimmedText()
    {
        string hash = PlaybookBaseStore.Hash("one\r\ntwo\r\n\r\n");

        Assert.Equal(16, hash.Length);
        Assert.Matches("^[0-9a-f]{16}$", hash);
        Assert.Equal(hash, PlaybookBaseStore.Hash("  one\ntwo"));
        Assert.NotEqual(hash, PlaybookBaseStore.Hash("one\ntwo changed"));
    }

    /// <summary>Save keeps the text in <c>playbook-defaults/&lt;kind&gt;-&lt;hash&gt;.md</c> and Read returns it.</summary>
    [Fact]
    public void Save_WritesTheTextUnderTheKindAndHash_AndReadReturnsIt()
    {
        PlaybookBaseStore store = new(this.dataDir);

        string hash = store.Save(PlaybookKind.Manager, "the old default");

        string expectedPath = Path.Combine(this.dataDir, "playbook-defaults", $"manager-{hash}.md");
        Assert.Equal(expectedPath, store.PathFor(PlaybookKind.Manager, hash));
        Assert.True(File.Exists(expectedPath));
        Assert.Equal("the old default", store.Read(PlaybookKind.Manager, hash));
        Assert.Null(store.Read(PlaybookKind.Member, hash));
    }

    /// <summary>A base file is never edited: saving the same hash again leaves the file as it was.</summary>
    [Fact]
    public void Save_ForAHashThatIsStored_LeavesTheFileAsItWas()
    {
        PlaybookBaseStore store = new(this.dataDir);
        string hash = store.Save(PlaybookKind.Member, "text");
        string path = store.PathFor(PlaybookKind.Member, hash);
        File.SetLastWriteTimeUtc(path, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        string again = store.Save(PlaybookKind.Member, "text");

        Assert.Equal(hash, again);
        Assert.Equal(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), File.GetLastWriteTimeUtc(path));
    }

    /// <summary>A base that was never stored reads as nothing.</summary>
    [Fact]
    public void Read_OfAMissingBase_IsNull()
    {
        PlaybookBaseStore store = new(this.dataDir);

        Assert.Null(store.Read(PlaybookKind.Member, "0123456789abcdef"));
    }
}
