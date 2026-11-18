using GoldfishReadDeny = Agency.Huddle.App.Goldfish.GoldfishReadDeny;

namespace Agency.Huddle.Tests.Goldfish;

/// <summary>
/// Pins the exact <c>Read(...)</c> deny strings a goldfish session is started with (spike S-1: the absolute form is <c>//&lt;drive&gt;/path/**</c>,
/// forward slashes, lowercase drive, and only a folder that exists).
/// </summary>
public sealed class GoldfishReadDenyTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("goldfish-deny-").FullName;

    /// <summary>Removes the temp folder.</summary>
    public void Dispose() => Directory.Delete(this.root, recursive: true);

    /// <summary>Creates a folder under the temp root and returns its full path.</summary>
    /// <param name="name">The folder name.</param>
    private string Folder(string name) => Directory.CreateDirectory(Path.Combine(this.root, name)).FullName;

    /// <summary>The rule the spec form gives for an existing folder: <c>//e/dir/**</c> on Windows, <c>//dir/**</c> from a rooted path elsewhere.</summary>
    /// <param name="folder">The folder's full path.</param>
    private static string Expected(string folder)
    {
        string slashed = folder.Replace('\\', '/').TrimEnd('/');
        string absolute = OperatingSystem.IsWindows()
            ? string.Concat(char.ToLowerInvariant(slashed[0]).ToString(), slashed.AsSpan(2))
            : slashed;
        return $"Read(//{absolute.TrimStart('/')}/**)";
    }

    /// <summary>An existing folder becomes one rule: <c>Read(//</c> the path with forward slashes, a lowercase drive and no colon, then <c>/**)</c>.</summary>
    [Fact]
    public void Rules_ExistingFolder_GivesTheExactDoubleSlashRule()
    {
        string rooms = this.Folder("rooms");

        IReadOnlyList<string> rules = GoldfishReadDeny.Rules([rooms]);

        string rule = Assert.Single(rules);
        Assert.Equal(Expected(rooms), rule);
        Assert.DoesNotContain('\\', rule);
        Assert.EndsWith("/rooms/**)", rule, StringComparison.Ordinal); // contains-ok: the exact rule is pinned by the Assert.Equal beside it
    }

    /// <summary>A folder that does not exist is dropped, and the one that does is kept in order.</summary>
    [Fact]
    public void Rules_MissingFolder_IsDropped()
    {
        string memory = this.Folder("memory");
        string missing = Path.Combine(this.root, "never-made");

        IReadOnlyList<string> rules = GoldfishReadDeny.Rules([missing, memory]);

        Assert.Equal([Expected(memory)], rules);
    }

    /// <summary>Upper- and lowercase drive letters in the input give the same lowercase-drive rule, once.</summary>
    [Fact]
    public void Rules_DriveLetterCase_IsLowercasedAndDeduplicated()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Drive letters exist only on Windows.");
        string rooms = this.Folder("rooms");
        string upper = string.Concat(char.ToUpperInvariant(rooms[0]).ToString(), rooms.AsSpan(1));
        string lower = string.Concat(char.ToLowerInvariant(rooms[0]).ToString(), rooms.AsSpan(1));

        IReadOnlyList<string> rules = GoldfishReadDeny.Rules([upper, lower]);

        string rule = Assert.Single(rules);
        Assert.StartsWith($"Read(//{char.ToLowerInvariant(rooms[0])}/", rule, StringComparison.Ordinal); // contains-ok: the exact rule is pinned by the Assert.Equal beside it
        Assert.Equal(Expected(rooms), rule);
    }

    /// <summary>No folders, no rules.</summary>
    [Fact]
    public void Rules_NoFolders_IsEmpty()
    {
        Assert.Empty(GoldfishReadDeny.Rules([]));
    }

    /// <summary>A trailing separator does not leave a doubled slash before <c>**</c>.</summary>
    [Fact]
    public void Rules_TrailingSeparator_GivesTheSameRule()
    {
        string rooms = this.Folder("rooms");

        IReadOnlyList<string> rules = GoldfishReadDeny.Rules([rooms + Path.DirectorySeparatorChar]);

        Assert.Equal([Expected(rooms)], rules);
        Assert.DoesNotContain("//**", rules[0], StringComparison.Ordinal);
    }

    /// <summary>The same folder given twice, in two spellings, yields one rule; a second folder keeps its own, in first-seen order, with no middle <c>**</c>.</summary>
    [Fact]
    public void Rules_RepeatedFolder_IsDeduplicatedInOrder()
    {
        string rooms = this.Folder("rooms");
        string memory = this.Folder("memory");
        string viaDots = Path.Combine(rooms, "..", "rooms");

        IReadOnlyList<string> rules = GoldfishReadDeny.Rules([rooms, memory, viaDots, rooms]);

        Assert.Equal([Expected(rooms), Expected(memory)], rules);
        Assert.All(rules, rule => Assert.Equal(rule.Length - 3, rule.IndexOf("**", StringComparison.Ordinal)));
    }
}
