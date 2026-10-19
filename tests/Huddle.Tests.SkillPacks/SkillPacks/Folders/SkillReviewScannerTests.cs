using Agency.Huddle.SkillPacks.Folders;
using System.Text;

namespace Agency.Huddle.Tests.SkillPacks.Folders;

/// <summary>
/// Pins the pre-install review scanner (SK-6, NF-4, D5): one flag per kind, prose links left alone,
/// the license warning, excerpt trimming and the result order.
/// </summary>
public sealed class SkillReviewScannerTests
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

    /// <summary>A skill with only prose, a plain link and a license has nothing to flag.</summary>
    [Fact]
    public void Scan_CleanSkill_NoFlags()
    {
        Dictionary<string, byte[]> files = Text(("SKILL.md", "# Notes\r\nSee https://example.com/docs for more.\r\nWork in the project folder.\r\n"));

        IReadOnlyList<ReviewFlag> flags = SkillReviewScanner.Scan(files, "MIT");

        Assert.Empty(flags);
    }

    /// <summary>Every executable or script extension is flagged as a Script on line 1 with the file name as excerpt.</summary>
    [Theory]
    [InlineData("tools/run.ps1")]
    [InlineData("setup.sh")]
    [InlineData("a.bash")]
    [InlineData("a.bat")]
    [InlineData("a.cmd")]
    [InlineData("a.py")]
    [InlineData("a.js")]
    [InlineData("a.ts")]
    [InlineData("a.mjs")]
    [InlineData("a.exe")]
    [InlineData("bin/a.dll")]
    [InlineData("TOOLS/RUN.PS1")]
    public void Scan_ScriptExtension_FlagsScript(string path)
    {
        Dictionary<string, byte[]> files = Text((path, "echo hi"));

        IReadOnlyList<ReviewFlag> flags = SkillReviewScanner.Scan(files, "MIT");

        ReviewFlag flag = Assert.Single(flags);
        Assert.Equal(ReviewFlagKind.Script, flag.Kind);
        Assert.Equal(path, flag.File);
        Assert.Equal(1, flag.Line);
        Assert.Equal(Path.GetFileName(path), flag.Excerpt);
    }

    /// <summary>Each listed command starting a fenced line is a ShellCommand at its line.</summary>
    [Theory]
    [InlineData("curl -s x")]
    [InlineData("wget x")]
    [InlineData("Invoke-WebRequest x")]
    [InlineData("Invoke-RestMethod x")]
    [InlineData("iwr x")]
    [InlineData("npm install")]
    [InlineData("pip install x")]
    [InlineData("git clone x")]
    [InlineData("rm -rf build")]
    [InlineData("Remove-Item x")]
    [InlineData("sudo reboot")]
    [InlineData("$ npm install")]
    [InlineData("> pip install x")]
    public void Scan_FencedCommand_FlagsShellCommand(string command)
    {
        Dictionary<string, byte[]> files = Text(("SKILL.md", "Run:\r\n```\r\n" + command + "\r\n```\r\n"));

        IReadOnlyList<ReviewFlag> flags = SkillReviewScanner.Scan(files, "MIT");

        ReviewFlag flag = Assert.Single(flags, f => f.Kind == ReviewFlagKind.ShellCommand);
        Assert.Equal("SKILL.md", flag.File);
        Assert.Equal(3, flag.Line);
        Assert.Equal(command, flag.Excerpt);
    }

    /// <summary>An indented command outside a fence is a ShellCommand; the same words in prose are not.</summary>
    [Fact]
    public void Scan_IndentedCommandFlaggedProseNot()
    {
        Dictionary<string, byte[]> files = Text(("SKILL.md", "Use curl to fetch things.\r\n    sudo make install\r\nnpm is a tool.\r\n"));

        IReadOnlyList<ReviewFlag> flags = SkillReviewScanner.Scan(files, "MIT");

        ReviewFlag flag = Assert.Single(flags);
        Assert.Equal(ReviewFlagKind.ShellCommand, flag.Kind);
        Assert.Equal(2, flag.Line);
        Assert.Equal("sudo make install", flag.Excerpt);
    }

    /// <summary>A URL inside a fenced block is a NetworkCall.</summary>
    [Fact]
    public void Scan_UrlInFence_FlagsNetworkCall()
    {
        Dictionary<string, byte[]> files = Text(("SKILL.md", "```\r\nconst u = \"https://api.example.com/v1\";\r\n```\r\n"));

        IReadOnlyList<ReviewFlag> flags = SkillReviewScanner.Scan(files, "MIT");

        ReviewFlag flag = Assert.Single(flags);
        Assert.Equal(ReviewFlagKind.NetworkCall, flag.Kind);
        Assert.Equal(2, flag.Line);
        Assert.Equal("const u = \"https://api.example.com/v1\";", flag.Excerpt);
    }

    /// <summary>A URL after a command yields both a ShellCommand and a NetworkCall for that line.</summary>
    [Fact]
    public void Scan_UrlAfterCommand_FlagsBoth()
    {
        Dictionary<string, byte[]> files = Text(("SKILL.md", "    curl http://example.com/x\r\n"));

        IReadOnlyList<ReviewFlag> flags = SkillReviewScanner.Scan(files, "MIT");

        Assert.Equal([ReviewFlagKind.ShellCommand, ReviewFlagKind.NetworkCall], flags.Select(f => f.Kind).ToArray());
    }

    /// <summary>A link in plain prose, or after the fence has closed, is not flagged.</summary>
    [Fact]
    public void Scan_ProseLinkAndAfterFence_NotFlagged()
    {
        Dictionary<string, byte[]> files = Text(("SKILL.md", "```\r\nplain text\r\n```\r\nSee http://example.com/a and [b](https://example.com/b).\r\n"));

        IReadOnlyList<ReviewFlag> flags = SkillReviewScanner.Scan(files, "MIT");

        Assert.Empty(flags);
    }

    /// <summary>Each outside-the-Work-Dir path prefix is a PathOutsideWorkDir.</summary>
    [Theory]
    [InlineData("/etc/hosts")]
    [InlineData("/usr/local/bin")]
    [InlineData("C:\\Windows\\System32")]
    [InlineData("~/notes")]
    [InlineData("$HOME/.ssh")]
    [InlineData("%USERPROFILE%\\x")]
    [InlineData("..\\secret")]
    [InlineData("../secret")]
    public void Scan_OutsidePath_FlagsPath(string path)
    {
        Dictionary<string, byte[]> files = Text(("SKILL.md", "Intro\r\nRead `" + path + "` now.\r\n"));

        IReadOnlyList<ReviewFlag> flags = SkillReviewScanner.Scan(files, "MIT");

        ReviewFlag flag = Assert.Single(flags);
        Assert.Equal(ReviewFlagKind.PathOutsideWorkDir, flag.Kind);
        Assert.Equal(2, flag.Line);
        Assert.Equal("Read `" + path + "` now.", flag.Excerpt);
    }

    /// <summary>Words that merely begin like a protected path, and relative in-folder paths, are not flagged.</summary>
    [Fact]
    public void Scan_LookalikePaths_NotFlagged()
    {
        Dictionary<string, byte[]> files = Text(("SKILL.md", "Use /etcetera and docs/etc/x and ./local/file and https://example.com/../x.\r\n"));

        IReadOnlyList<ReviewFlag> flags = SkillReviewScanner.Scan(files, "MIT");

        Assert.Empty(flags);
    }

    /// <summary>A missing or "none" license gives one NoLicense flag on file "."; a real one gives none.</summary>
    [Theory]
    [InlineData("none", true)]
    [InlineData("None", true)]
    [InlineData("", true)]
    [InlineData("  ", true)]
    [InlineData("MIT", false)]
    public void Scan_License_FlagsOnlyWhenMissing(string license, bool flagged)
    {
        Dictionary<string, byte[]> files = Text(("SKILL.md", "Hello"));

        IReadOnlyList<ReviewFlag> flags = SkillReviewScanner.Scan(files, license);

        if (!flagged)
        {
            Assert.Empty(flags);
            return;
        }

        ReviewFlag flag = Assert.Single(flags);
        Assert.Equal(ReviewFlagKind.NoLicense, flag.Kind);
        Assert.Equal(".", flag.File);
        Assert.Equal(1, flag.Line);
        Assert.Equal("No license declared", flag.Excerpt);
    }

    /// <summary>An excerpt longer than 120 characters is cut to exactly 120.</summary>
    [Fact]
    public void Scan_LongLine_ExcerptTrimmedTo120()
    {
        string line = "    curl " + new string('x', 200);
        Dictionary<string, byte[]> files = Text(("SKILL.md", line + "\r\n"));

        IReadOnlyList<ReviewFlag> flags = SkillReviewScanner.Scan(files, "MIT");

        ReviewFlag flag = Assert.Single(flags);
        Assert.Equal(line.Trim()[..120], flag.Excerpt);
    }

    /// <summary>Flags come ordered by kind, then file, then line, whatever the input order.</summary>
    [Fact]
    public void Scan_MixedFindings_OrderedByKindFileLine()
    {
        Dictionary<string, byte[]> files = Text(
            ("z.md", "```\r\nnpm i\r\n```\r\n"),
            ("b/run.sh", "x"),
            ("a.md", "go to ~/x\r\n```\r\ncurl q\r\n```\r\n```\r\nsudo y\r\n```\r\n"),
            ("a/run.py", "x"));

        IReadOnlyList<ReviewFlag> flags = SkillReviewScanner.Scan(files, "none");

        string[] actual = flags.Select(f => string.Concat(f.Kind.ToString(), "|", f.File, "|", f.Line.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToArray();
        Assert.Equal(
            [
                "Script|a/run.py|1",
                "Script|b/run.sh|1",
                "ShellCommand|a.md|3",
                "ShellCommand|a.md|6",
                "ShellCommand|z.md|2",
                "PathOutsideWorkDir|a.md|1",
                "NoLicense|.|1"
            ],
            actual);
    }
}
