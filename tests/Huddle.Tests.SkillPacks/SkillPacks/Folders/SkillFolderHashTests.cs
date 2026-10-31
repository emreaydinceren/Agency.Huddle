using Agency.Huddle.SkillPacks.Folders;
using System.Text;

namespace Agency.Huddle.Tests.SkillPacks.Folders;

/// <summary>
/// Pins the stable skill-folder hash (SK-7, SK-9, NF-4): order independence, sensitivity to names and bytes,
/// the ignored <c>.source.json</c>, line-ending normalisation for text files only, parity between the
/// in-memory and on-disk entry points, and a fixed vector that pins the algorithm.
/// </summary>
public sealed class SkillFolderHashTests
{
    /// <summary>Builds a file map from UTF-8 text.</summary>
    private static Dictionary<string, byte[]> Text(params (string Path, string Content)[] entries)
    {
        Dictionary<string, byte[]> files = new(StringComparer.Ordinal);
        foreach ((string path, string content) in entries)
        {
            files[path] = Encoding.UTF8.GetBytes(content);
        }

        return files;
    }

    /// <summary>The same files inserted in a different order hash equal.</summary>
    [Fact]
    public void Compute_DifferentInsertionOrder_SameHash()
    {
        Dictionary<string, byte[]> first = Text(("SKILL.md", "a"), ("notes/b.md", "b"), ("c.txt", "c"));
        Dictionary<string, byte[]> second = Text(("c.txt", "c"), ("notes/b.md", "b"), ("SKILL.md", "a"));

        Assert.Equal(SkillFolderHash.Compute(first), SkillFolderHash.Compute(second));
    }

    /// <summary>Renaming a file changes the hash.</summary>
    [Fact]
    public void Compute_RenamedFile_ChangesHash()
    {
        string before = SkillFolderHash.Compute(Text(("SKILL.md", "a")));
        string after = SkillFolderHash.Compute(Text(("OTHER.md", "a")));

        Assert.NotEqual(before, after);
    }

    /// <summary>Changing one byte of one file changes the hash.</summary>
    [Fact]
    public void Compute_OneByteChanged_ChangesHash()
    {
        string before = SkillFolderHash.Compute(Text(("SKILL.md", "abc")));
        string after = SkillFolderHash.Compute(Text(("SKILL.md", "abd")));

        Assert.NotEqual(before, after);
    }

    /// <summary>A <c>.source.json</c> entry, present or with different content, does not affect the hash.</summary>
    [Fact]
    public void Compute_SourceJson_IsIgnored()
    {
        string plain = SkillFolderHash.Compute(Text(("SKILL.md", "a")));
        string withSource = SkillFolderHash.Compute(Text(("SKILL.md", "a"), (".source.json", "{\"x\":1}")));
        string otherSource = SkillFolderHash.Compute(Text(("SKILL.md", "a"), (".source.json", "{\"x\":2}")));

        Assert.Equal(plain, withSource);
        Assert.Equal(plain, otherSource);
    }

    /// <summary>CRLF and LF text files with a normalised extension hash equal.</summary>
    [Theory]
    [InlineData("SKILL.md")]
    [InlineData("a.txt")]
    [InlineData("a.json")]
    [InlineData("a.yml")]
    [InlineData("a.yaml")]
    public void Compute_TextFileCrlfVersusLf_HashesEqual(string path)
    {
        string crlf = SkillFolderHash.Compute(Text((path, "one\r\ntwo\r\n")));
        string lf = SkillFolderHash.Compute(Text((path, "one\ntwo\n")));

        Assert.Equal(lf, crlf);
    }

    /// <summary>A binary file keeps its CRLF bytes, so CRLF and LF variants hash differently.</summary>
    [Fact]
    public void Compute_BinaryFileCrlfVersusLf_HashesDiffer()
    {
        Dictionary<string, byte[]> crlf = new(StringComparer.Ordinal) { ["a.bin"] = [1, 13, 10, 2] };
        Dictionary<string, byte[]> lf = new(StringComparer.Ordinal) { ["a.bin"] = [1, 10, 2] };

        Assert.NotEqual(SkillFolderHash.Compute(lf), SkillFolderHash.Compute(crlf));
    }

    /// <summary>Hashing a folder on disk equals hashing the same files in memory, ignoring <c>.source.json</c>.</summary>
    [Fact]
    public void ComputeFolder_EqualsComputeOfSameFiles()
    {
        using TempDataDir dir = new();
        string folder = Path.Combine(dir.Path, "skill");
        Directory.CreateDirectory(Path.Combine(folder, "scripts"));
        File.WriteAllBytes(Path.Combine(folder, "SKILL.md"), Encoding.UTF8.GetBytes("# Title\r\nbody\r\n"));
        File.WriteAllBytes(Path.Combine(folder, "scripts", "run.bin"), [0, 13, 10, 255]);
        File.WriteAllText(Path.Combine(folder, ".source.json"), "{}");
        Dictionary<string, byte[]> files = new(StringComparer.Ordinal)
        {
            ["SKILL.md"] = Encoding.UTF8.GetBytes("# Title\nbody\n"),
            ["scripts/run.bin"] = [0, 13, 10, 255],
        };

        Assert.Equal(SkillFolderHash.Compute(files), SkillFolderHash.ComputeFolder(folder));
    }

    /// <summary>A <c>.git</c> folder inside the Skill, as when the Skill is the root of its own clone, does not change the hash.</summary>
    [Fact]
    public void ComputeFolder_GitFolder_IsIgnored()
    {
        using TempDataDir dir = new();
        string folder = Path.Combine(dir.Path, "skill");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "SKILL.md"), "a");
        string before = SkillFolderHash.ComputeFolder(folder);
        Directory.CreateDirectory(Path.Combine(folder, ".git", "objects"));
        File.WriteAllText(Path.Combine(folder, ".git", "HEAD"), "ref: refs/heads/main");
        File.WriteAllText(Path.Combine(folder, ".git", "objects", "ab"), "blob");

        string after = SkillFolderHash.ComputeFolder(folder);

        Assert.Equal(before, after);
    }

    /// <summary>A fixed input hashes to a fixed lowercase hex string, pinning the algorithm.</summary>
    [Fact]
    public void Compute_FixedVector_MatchesPinnedHash()
    {
        Dictionary<string, byte[]> files = new(StringComparer.Ordinal)
        {
            ["scripts/run.bin"] = [0, 13, 10, 255],
            ["SKILL.md"] = Encoding.UTF8.GetBytes("# Title\nbody\n"),
        };

        Assert.Equal("752a8a455203cae7a2f22b0d4b8defbc70f41da4f3bdb86baab248a9a71dfe90", SkillFolderHash.Compute(files));
    }
}
