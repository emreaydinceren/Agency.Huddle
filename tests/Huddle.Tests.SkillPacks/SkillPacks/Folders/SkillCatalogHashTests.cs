using System.Text;
using Agency.Huddle.SkillPacks.Folders;

namespace Agency.Huddle.Tests.SkillPacks.Folders;

/// <summary>
/// Pins the folder hash the catalog's <c>sha256</c> is compared with: line endings do not matter in any text
/// file, binary files are hashed as they are, and a real change still shows. The hash recorded in
/// <c>.source.json</c> ("modified locally") keeps its own rule and is pinned in <see cref="SkillFolderHashTests"/>.
/// </summary>
public sealed class SkillCatalogHashTests
{
    /// <summary>A file map from UTF-8 text.</summary>
    private static Dictionary<string, byte[]> Text(params (string Path, string Content)[] entries)
    {
        Dictionary<string, byte[]> files = new(StringComparer.Ordinal);
        foreach ((string path, string content) in entries)
        {
            files[path] = Encoding.UTF8.GetBytes(content);
        }

        return files;
    }

    /// <summary>A clone that turned a shell script and a Python file into CRLF (core.autocrlf=true) still matches the original.</summary>
    [Fact]
    public void ComputeCatalog_CrlfInScriptFiles_MatchesLfOriginal()
    {
        Dictionary<string, byte[]> original = Text(("SKILL.md", "a\nb\n"), ("run.sh", "echo 1\necho 2\n"), ("tool.py", "x = 1\n"), ("page.html", "<p>\n</p>\n"));
        Dictionary<string, byte[]> cloned = Text(("SKILL.md", "a\r\nb\r\n"), ("run.sh", "echo 1\r\necho 2\r\n"), ("tool.py", "x = 1\r\n"), ("page.html", "<p>\r\n</p>\r\n"));

        Assert.Equal(SkillFolderHash.ComputeCatalog(original), SkillFolderHash.ComputeCatalog(cloned));
    }

    /// <summary>The "modified locally" hash is unchanged: a CRLF script still differs there, so recorded hashes keep their meaning.</summary>
    [Fact]
    public void Compute_CrlfInShellScript_StillDiffers()
    {
        string lf = SkillFolderHash.Compute(Text(("run.sh", "echo 1\n")));
        string crlf = SkillFolderHash.Compute(Text(("run.sh", "echo 1\r\n")));

        Assert.NotEqual(lf, crlf);
    }

    /// <summary>A file with a NUL in its first 8000 bytes is binary and is hashed byte for byte, CRLF bytes included.</summary>
    [Fact]
    public void ComputeCatalog_BinaryFileWithCrlfBytes_IsNotNormalised()
    {
        Dictionary<string, byte[]> lf = new(StringComparer.Ordinal) { ["blob.bin"] = [1, 0, 10, 2] };
        Dictionary<string, byte[]> crlf = new(StringComparer.Ordinal) { ["blob.bin"] = [1, 0, 13, 10, 2] };

        Assert.NotEqual(SkillFolderHash.ComputeCatalog(lf), SkillFolderHash.ComputeCatalog(crlf));
    }

    /// <summary>A NUL after byte 8000 does not make a file binary, so its CRLF is still read as LF.</summary>
    [Fact]
    public void ComputeCatalog_NulBeyondFirstEightThousandBytes_IsStillText()
    {
        byte[] head = [.. Enumerable.Repeat((byte)'a', 8000)];
        Dictionary<string, byte[]> lf = new(StringComparer.Ordinal) { ["x.dat"] = [.. head, 10, 0] };
        Dictionary<string, byte[]> crlf = new(StringComparer.Ordinal) { ["x.dat"] = [.. head, 13, 10, 0] };

        Assert.Equal(SkillFolderHash.ComputeCatalog(lf), SkillFolderHash.ComputeCatalog(crlf));
    }

    /// <summary>One changed character in a script still gives a different hash, so a real edit reads as Altered.</summary>
    [Fact]
    public void ComputeCatalog_OneCharacterChange_Differs()
    {
        string original = SkillFolderHash.ComputeCatalog(Text(("SKILL.md", "a\n"), ("run.sh", "echo 1\n")));
        string edited = SkillFolderHash.ComputeCatalog(Text(("SKILL.md", "a\n"), ("run.sh", "echo 2\n")));

        Assert.NotEqual(original, edited);
    }

    /// <summary>The folder form skips <c>.source.json</c> and a <c>.git</c> folder like the other hash.</summary>
    [Fact]
    public void ComputeCatalog_SourceJsonAndGitFolder_AreIgnored()
    {
        Dictionary<string, byte[]> plain = Text(("SKILL.md", "a\n"));
        Dictionary<string, byte[]> withExtras = Text(("SKILL.md", "a\n"), (".source.json", "{}"), (".git/config", "x"));

        Assert.Equal(SkillFolderHash.ComputeCatalog(plain), SkillFolderHash.ComputeCatalog(withExtras));
    }
}
