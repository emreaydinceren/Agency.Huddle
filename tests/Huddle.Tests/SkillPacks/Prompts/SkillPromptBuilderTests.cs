using System.Text.Json;
using System.Text.RegularExpressions;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.SkillSources;
using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.Tests.SkillPacks.Prompts;

/// <summary>Pins <see cref="SkillPromptBuilder"/> (§6.8): every placeholder filled, typed text one capped line and never
/// expanded, the purpose, sources, problems and missing rules, and a quoted install path with spaces.</summary>
public sealed partial class SkillPromptBuilderTests
{
    private const string InstallFile = @"E:\Huddle\App_Data\InstallSkillPack.md";

    /// <summary>A builder over the defaults.</summary>
    private static SkillPromptBuilder Builder(string path = InstallFile)
    {
        return new SkillPromptBuilder(new RecordingPromptSource(), new FixedInstallFile(path));
    }

    /// <summary>Add fills where and the purpose line, and starts by telling the tool to read the install file.</summary>
    [Fact]
    public void Add_FillsWhereAndPurpose()
    {
        string prompt = Builder().Add("https://github.com/anthropics/skills", "Word and Excel files.");

        Assert.StartsWith($"Read the file at {InstallFile} first", prompt, StringComparison.Ordinal);
        Assert.Contains("Task: add Packs or Skills from https://github.com/anthropics/skills (section 9, \"Add\").", prompt, StringComparison.Ordinal);
        Assert.Contains("What they are for: Word and Excel files.\n", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", prompt, StringComparison.Ordinal);
    }

    /// <summary>A blank purpose leaves no gap of three line breaks; a blank where throws.</summary>
    [Fact]
    public void Add_BlankPurpose_NoGap_BlankWhereThrows()
    {
        string prompt = Builder().Add("E:\\Clones\\x", " ");

        Assert.DoesNotContain("\n\n\n", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("What they are for", prompt, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => Builder().Add("  ", null));
    }

    /// <summary>Typed text is one line, capped at 500 characters, and a <c>{{token}}</c> inside it is never expanded.</summary>
    [Fact]
    public void TypedText_OneLineCappedNeverExpanded()
    {
        string where = "line one\nline two {{installFile}} " + new string('x', 600);

        string prompt = Builder().Add(where, null);

        string line = prompt.Split('\n').Single(static line => line.StartsWith("Task:", StringComparison.Ordinal));
        Assert.Contains("line one line two {{installFile}} ", line, StringComparison.Ordinal);
        Assert.Contains("…", line, StringComparison.Ordinal);
    }

    /// <summary>An install path with a space is quoted in the prompt but not by Copy path.</summary>
    [Fact]
    public void InstallFile_WithSpace_QuotedInPromptOnly()
    {
        SkillPromptBuilder builder = Builder(@"E:\My Files\InstallSkillPack.md");

        Assert.StartsWith("Read the file at \"E:\\My Files\\InstallSkillPack.md\" first", builder.Add("x", null), StringComparison.Ordinal);
        Assert.Equal(@"E:\My Files\InstallSkillPack.md", builder.InstallFilePath(PathFormat.Windows));
    }

    /// <summary>Find for a role fills the id and title and doubles no final full stop.</summary>
    [Fact]
    public void FindForRole_FillsRoleAndPurpose()
    {
        string prompt = Builder().FindForRole("developer", "Developer", "reviews.");

        Assert.Contains("find Packs for the Developer role (id: developer)", prompt, StringComparison.Ordinal);
        Assert.Contains("It should get better at: reviews.\n", prompt, StringComparison.Ordinal);
        Assert.Contains("with developer in appliesTo", prompt, StringComparison.Ordinal);
    }

    /// <summary>No Source ids means every Source; ids become one line each.</summary>
    [Fact]
    public void Update_SourcesRule()
    {
        Assert.Contains("all the Sources in SKILLS.md", Builder().Update([]), StringComparison.Ordinal);
        Assert.Contains("- github.com/a/b\n- local/c\n", Builder().Update(["github.com/a/b", "local/c"]), StringComparison.Ordinal);
    }

    /// <summary>Problems are errors first, indented, at most 20 with a count of the rest; none says so.</summary>
    [Fact]
    public void Fix_ProblemsRule()
    {
        List<Finding> findings = [Finding.Warning("S008", @"E:\c\docx", "scripts/ is never read")];
        findings.AddRange(Enumerable.Range(1, 21).Select(static i => Finding.Error("P005", @"E:\p\a.skillpack.json", $"problem {i}")));

        string prompt = Builder().Fix("Packs", "packs/a.skillpack.json", "Broken", findings);
        string none = Builder().Fix("Packs", "packs/a.skillpack.json", "Broken", []);

        string[] lines = prompt.Split('\n');
        Assert.StartsWith("    ERROR P005", lines.First(static line => line.StartsWith("    ", StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.Contains("    … and 2 more; run validate on the row's path.", prompt, StringComparison.Ordinal);
        Assert.Equal(20, lines.Count(static line => line.StartsWith("    ERROR", StringComparison.Ordinal) || line.StartsWith("    WARN", StringComparison.Ordinal)));
        Assert.Contains("    (none recorded)", none, StringComparison.Ordinal);
    }

    /// <summary>Fetch one Skill fills the fall-backs for a missing catalog path and origin.</summary>
    [Fact]
    public void FetchSkill_Fallbacks()
    {
        string prompt = Builder().FetchSkill("docx", "anthropics-skills", null, null);

        Assert.Contains("Catalog: anthropics-skills, at (no path on this computer)", prompt, StringComparison.Ordinal);
        Assert.Contains("Origin: (unknown)", prompt, StringComparison.Ordinal);
    }

    /// <summary>Needs setup writes one line per missing Pack or Skill.</summary>
    [Fact]
    public void NeedsSetup_MissingRule()
    {
        MissingItem[] missing =
        [
            new("msbuild-authoring", true, ItemState.NotDownloaded, "r", "MSBuild Authoring", null),
            new("code-review-and-quality", false, ItemState.NotDownloaded, "r", "code-review-and-quality", "addyosmani-agent-skills"),
            new("orphan", false, ItemState.Broken, "r", "orphan", null),
        ];

        string prompt = Builder().NeedsSetup("reviewer", "Reviewer", missing);

        Assert.Contains("Pack \"MSBuild Authoring\" (msbuild-authoring)\nSkill \"code-review-and-quality\" (catalog addyosmani-agent-skills)\nSkill \"orphan\" (not in any catalog)", prompt, StringComparison.Ordinal);
    }

    /// <summary>Every default starts by telling the tool to read the install file, is 5 to 15 lines, and leaves no
    /// placeholder once a builder method has rendered it.</summary>
    [Fact]
    public void Defaults_StartWithInstallFileAndAreShort()
    {
        SkillPromptBuilder builder = Builder();
        string[] prompts =
        [
            builder.Add("x", "y"),
            builder.FindForRole("dev", "Developer", "z"),
            builder.Update([]),
            builder.Fix("Packs", "p", "Broken", []),
            builder.FetchSkill("docx", "c", "p", "o"),
            builder.NeedsSetup("dev", "Developer", [new MissingItem("a", true, ItemState.NotDownloaded, "r", "A", null)]),
        ];

        Assert.All(prompts, static prompt =>
        {
            Assert.StartsWith("Read the file at ", prompt, StringComparison.Ordinal);
            Assert.InRange(prompt.Split('\n').Length, 5, 15);
            Assert.DoesNotMatch(Placeholder(), prompt);
        });
    }

    /// <summary>The seven keys are in prompts.default.json and sit in the two new Settings groups.</summary>
    [Fact]
    public void Defaults_InDefaultsFileAndGrouped()
    {
        string[] keys = ["installSkillPack.body", "skillPrompt.add", "skillPrompt.findForRole", "skillPrompt.update", "skillPrompt.fix", "skillPrompt.fetchSkill", "skillPrompt.needsSetup"];
        string file = Path.Combine(AppContext.BaseDirectory, "prompts.default.json");
        using JsonDocument defaults = JsonDocument.Parse(File.ReadAllText(file));

        Assert.All(keys, key =>
        {
            Assert.True(defaults.RootElement.TryGetProperty(key, out _), key);
            PromptDefinition definition = PromptCatalog.All.Single(item => item.Key == key);
            Assert.All(definition.Placeholders, static placeholder => Assert.Matches(Placeholder(), placeholder));
            Assert.Subset(definition.Placeholders.ToHashSet(StringComparer.Ordinal), definition.RequiredPlaceholders.ToHashSet(StringComparer.Ordinal));
        });
    }

    [GeneratedRegex(@"\{\{[a-zA-Z]+\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();
}

/// <summary>Pins <see cref="PathFormats"/> (§6.8, "Copy path formats"): the three formats and every edge case.</summary>
public sealed class PathFormatsTests
{
    /// <summary>The table of §6.8, one row per input and format.</summary>
    [Theory]
    [InlineData(@"E:\Huddle\App_Data\InstallSkillPack.md", PathFormat.Windows, @"E:\Huddle\App_Data\InstallSkillPack.md")]
    [InlineData(@"E:\Huddle\App_Data\InstallSkillPack.md", PathFormat.ForwardSlashes, "E:/Huddle/App_Data/InstallSkillPack.md")]
    [InlineData(@"E:\Huddle\App_Data\InstallSkillPack.md", PathFormat.Wsl, "/mnt/e/Huddle/App_Data/InstallSkillPack.md")]
    [InlineData(@"\\?\C:\x\f.md", PathFormat.Windows, @"C:\x\f.md")]
    [InlineData(@"\\?\C:\x\f.md", PathFormat.ForwardSlashes, "C:/x/f.md")]
    [InlineData(@"\\?\C:\x\f.md", PathFormat.Wsl, "/mnt/c/x/f.md")]
    [InlineData(@"\\server\share\f.md", PathFormat.Windows, @"\\server\share\f.md")]
    [InlineData(@"\\server\share\f.md", PathFormat.ForwardSlashes, "//server/share/f.md")]
    [InlineData(@"\\server\share\f.md", PathFormat.Wsl, "//server/share/f.md")]
    [InlineData(@"\\wsl$\Ubuntu\home\emre\f.md", PathFormat.Wsl, "/home/emre/f.md")]
    [InlineData(@"\\wsl.localhost\Ubuntu\home\emre\f.md", PathFormat.Wsl, "/home/emre/f.md")]
    [InlineData(@"\\wsl$\Ubuntu\home\emre\f.md", PathFormat.ForwardSlashes, "//wsl$/Ubuntu/home/emre/f.md")]
    [InlineData(@"C:\", PathFormat.Windows, @"C:\")]
    [InlineData(@"C:\", PathFormat.ForwardSlashes, "C:/")]
    [InlineData(@"C:\", PathFormat.Wsl, "/mnt/c/")]
    [InlineData("/home/me/f.md", PathFormat.Windows, "/home/me/f.md")]
    [InlineData("/home/me/f.md", PathFormat.Wsl, "/home/me/f.md")]
    [InlineData(@"E:\My Files\f.md", PathFormat.Wsl, "/mnt/e/My Files/f.md")]
    public void Convert_Table(string input, PathFormat format, string expected)
    {
        Assert.Equal(expected, PathFormats.Convert(input, format));
    }

    /// <summary>A path with no drive and no leading slash throws.</summary>
    [Fact]
    public void Convert_RelativePath_Throws()
    {
        Assert.Throws<ArgumentException>(() => PathFormats.Convert(@"relative\f.md", PathFormat.Windows));
    }
}
