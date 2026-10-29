using System.Text.Json;
using Agency.Huddle.Packs;
using Agency.Huddle.SkillPacks;
using Agency.Huddle.SkillPacks.Checking;
using static Agency.Huddle.Tests.SkillPacks.Checks.RegistryChecksTests;
using CheckRunner = Agency.Huddle.SkillPacks.Checking.Checks;
using PacksProgram = Agency.Huddle.Packs.Program;
using ResolveOptions = Agency.Huddle.SkillPacks.Resolving.ResolveOptions;
using SchemaFiles = Agency.Huddle.SkillPacks.Files.SchemaFiles;

namespace Agency.Huddle.Tests.SkillPacks.Cli;

/// <summary>Runs <c>huddle-packs</c> in-process over a <see cref="RegistryFixture"/> with its context file written.</summary>
internal static class PacksRunner
{
    /// <summary>The data folder of every run.</summary>
    public static string DataDir { get; } = InMemoryFileReader.At("Data");

    /// <summary>Writes the fixture's SKILLS.md and context file.</summary>
    public static RegistryFixture Ready(RegistryFixture fixture, string huddleVersion)
    {
        fixture.WriteSkillsMd();
        CheckContext context = fixture.Context(huddleVersion);
        InstallContext file = new(1, context.HuddleVersion, context.ComputerName, DataDir, context.SkillSourcesDir, context.SkillsDir, context.Roles, context.InstalledSkills, context.AppToolNames, context.Written);
        fixture.Files.Add(Path.Combine(DataDir, InstallContext.FileName), file.ToJson());
        return fixture;
    }

    /// <summary>Runs with <c>--data-dir</c> pointing at the fixture, and returns the exit code and both streams.</summary>
    public static (int Exit, string Output, string Error) Run(RegistryFixture fixture, params string[] args)
    {
        Ready(fixture, HelpText.Version);
        return RunRaw(fixture.Files, ["--data-dir", DataDir, .. args]);
    }

    /// <summary>Runs with exactly <paramref name="args"/>.</summary>
    public static (int Exit, string Output, string Error) RunRaw(IFileReader files, string[] args, Func<string, string?>? environment = null)
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int exit = PacksProgram.Run(args, output, error, environment ?? (static _ => null), InMemoryFileReader.At("cwd"), InMemoryFileReader.At("exe"), files);
        return (exit, output.ToString(), error.ToString());
    }

    /// <summary>The non-empty lines of a text.</summary>
    public static string[] Lines(string text)
    {
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

/// <summary>Pins the hand-rolled parser (§6.4): usage and exit 2 for bad input, options anywhere, last wins.</summary>
public sealed class ArgumentParserTests
{
    /// <summary>An unknown option, a missing value, no command, an unknown command, a second path and an unknown schema all exit 2 with the usage.</summary>
    [Theory]
    [InlineData("validate", "--bogus")]
    [InlineData("validate", "--data-dir")]
    [InlineData]
    [InlineData("frobnicate")]
    [InlineData("validate", "a", "b")]
    [InlineData("schema", "agent")]
    [InlineData("roles", "extra")]
    public void Parse_BadInput_UsageAndExit2(params string[] args)
    {
        (int exit, string output, string error) = PacksRunner.RunRaw(new InMemoryFileReader(), args);

        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.Contains("huddle-packs <command> [options]", error, StringComparison.Ordinal);
    }

    /// <summary>Options may come before or after the command, and a repeated option's last value wins.</summary>
    [Fact]
    public void Parse_OptionsAnywhere_LastWins()
    {
        using StringWriter error = new();

        ParsedArguments? parsed = ArgumentParser.Parse(["--json", "--data-dir", "a", "validate", "x", "--data-dir", "b", "--strict"], error);

        Assert.NotNull(parsed);
        Assert.Equal(new ParsedArguments("validate", "x", "b", Json: true, Quiet: false, Strict: true), parsed);
    }

    /// <summary><c>--version</c> prints the version and the schema version and needs no data.</summary>
    [Fact]
    public void Version_PrintsVersionAndSchema()
    {
        (int exit, string output, _) = PacksRunner.RunRaw(new InMemoryFileReader(), ["--version"]);

        Assert.Equal(0, exit);
        Assert.Equal($"huddle-packs {HelpText.Version} (schemaVersion 1)", output.Trim());
    }
}

/// <summary>Pins how the data folder, the context file and the Skill Sources folder are found (§6.4).</summary>
public sealed class DataLocatorTests
{
    /// <summary><c>--data-dir</c> wins over <c>HUDDLE_DATA_DIR</c>, which wins over the default.</summary>
    [Fact]
    public void DataDir_OptionOverEnvironmentOverDefault()
    {
        InMemoryFileReader files = new();
        string cwd = InMemoryFileReader.At("cwd");
        string exe = InMemoryFileReader.At("exe");
        static string? Env(string name) => name == DataLocator.EnvironmentVariable ? InMemoryFileReader.At("env") : null;

        Assert.Equal(InMemoryFileReader.At("opt"), DataLocator.DataDir(InMemoryFileReader.At("opt"), Env, cwd, exe, files));
        Assert.Equal(InMemoryFileReader.At("env"), DataLocator.DataDir(null, Env, cwd, exe, files));
        Assert.Equal(Path.Combine(cwd, "App_Data"), DataLocator.DataDir(null, static _ => null, cwd, exe, files));

        files.Add(Path.Combine(exe, "App_Data", InstallContext.FileName), "{}");
        Assert.Equal(Path.Combine(exe, "App_Data"), DataLocator.DataDir(null, static _ => null, cwd, exe, files));
    }

    /// <summary>The Skill Sources folder and the computer name come from the context file.</summary>
    [Fact]
    public void Locate_ReadsFolderAndComputerFromContext()
    {
        RegistryFixture fixture = Healthy();
        fixture.Computer = "LAPTOP";
        PacksRunner.Ready(fixture, "2.4.0");

        LocateResult located = DataLocator.Locate(PacksRunner.DataDir, fixture.Files);

        Assert.NotNull(located.Location);
        Assert.Equal(fixture.Dir, located.Location.SkillSourcesDir);
        Assert.Equal("LAPTOP", located.Location.Context.ComputerName);
    }
}

/// <summary>Pins the context-file errors of §6.4: the exact sentences and exit 2.</summary>
public sealed class CliContextTests
{
    /// <summary>No context file: the exact sentence on standard error, exit 2, nothing on standard output.</summary>
    [Fact]
    public void NoContextFile_ExactSentence()
    {
        InMemoryFileReader files = new InMemoryFileReader().AddDirectory(PacksRunner.DataDir);

        (int exit, string output, string error) = PacksRunner.RunRaw(files, ["validate", "--data-dir", PacksRunner.DataDir]);

        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.Equal($"No install-context.json in {PacksRunner.DataDir}. Start Huddle once so it writes it, then run this again.", error.Trim());
    }

    /// <summary>Invalid JSON and a newer schema version exit 2 with their sentences.</summary>
    [Fact]
    public void InvalidOrNewerContext_Exit2()
    {
        InMemoryFileReader broken = new InMemoryFileReader().Add(Path.Combine(PacksRunner.DataDir, InstallContext.FileName), "{ not json");
        string newer = ContextJson().Replace("\"schemaVersion\": 1", "\"schemaVersion\": 9", StringComparison.Ordinal);
        InMemoryFileReader future = new InMemoryFileReader().Add(Path.Combine(PacksRunner.DataDir, InstallContext.FileName), newer);

        (int firstExit, _, string firstError) = PacksRunner.RunRaw(broken, ["roles", "--data-dir", PacksRunner.DataDir]);
        (int secondExit, _, string secondError) = PacksRunner.RunRaw(future, ["roles", "--data-dir", PacksRunner.DataDir]);

        Assert.Equal(2, firstExit);
        Assert.StartsWith($"install-context.json in {PacksRunner.DataDir} cannot be read: not valid JSON.", firstError, StringComparison.Ordinal);
        Assert.Equal(2, secondExit);
        Assert.StartsWith("install-context.json was written for schemaVersion 9; this huddle-packs checks 1.", secondError, StringComparison.Ordinal);
    }

    /// <summary>A valid context file's JSON.</summary>
    private static string ContextJson()
    {
        return Install.ContextFileTests.Sample().ToJson();
    }
}

/// <summary>Pins <c>validate</c>'s lines, exit codes, JSON and path filter (§6.4).</summary>
public sealed class ValidateTests
{
    /// <summary>A healthy registry prints one OK line and the summary last, exit 0.</summary>
    [Fact]
    public void Validate_Healthy_OkLineAndSummaryLast()
    {
        (int exit, string output, string error) = PacksRunner.Run(Healthy(), "validate");

        string[] lines = PacksRunner.Lines(output);
        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal("OK           SKILLS.md  Packs row 1 points at packs/documents.skillpack.json", lines[0]);
        Assert.Equal("PACKS-VALIDATE Rows=1 Packs=1 Skills=1 Errors=0 Warnings=0", lines[^1]);
    }

    /// <summary>An error prints an ERROR line with the file relative to the Skill Sources folder; exit 1.</summary>
    [Fact]
    public void Validate_Error_RelativeFileAndExit1()
    {
        RegistryFixture fixture = new();
        fixture.Source().Pack("documents", """[{ "name": "docx", "path": "" }]""", appliesTo: """["accountant"]""");

        (int exit, string output, _) = PacksRunner.Run(fixture, "validate");

        Assert.Equal(1, exit);
        Assert.Contains(PacksRunner.Lines(output), static line => line.StartsWith("ERROR  P005  packs/documents.skillpack.json  appliesTo: \"accountant\"", StringComparison.Ordinal));
    }

    /// <summary>A warning keeps exit 0; <c>--strict</c> makes an S001 an error and exits 1.</summary>
    [Fact]
    public void Validate_Strict_TurnsS001IntoExit1()
    {
        RegistryFixture loose = new();
        loose.Source().Pack("documents", $$"""[{ "name": "docx", "path": "", "origin": "{{Pinned}}docx" }]""");
        RegistryFixture strict = new();
        strict.Source().Pack("documents", $$"""[{ "name": "docx", "path": "", "origin": "{{Pinned}}docx" }]""");

        (int looseExit, string looseOutput, _) = PacksRunner.Run(loose, "validate");
        (int strictExit, _, _) = PacksRunner.Run(strict, "validate", "--strict");

        Assert.Equal(0, looseExit);
        Assert.Contains(PacksRunner.Lines(looseOutput), static line => line.StartsWith("WARN   S001", StringComparison.Ordinal));
        Assert.Equal(1, strictExit);
    }

    /// <summary><c>--quiet</c> drops warning and OK lines but the summary still counts them.</summary>
    [Fact]
    public void Validate_Quiet_DropsWarningsKeepsCounts()
    {
        RegistryFixture fixture = new();
        fixture.Source().Pack("documents", $$"""[{ "name": "docx", "path": "", "origin": "{{Pinned}}docx" }]""");

        (_, string output, _) = PacksRunner.Run(fixture, "validate", "--quiet");

        Assert.Equal(["PACKS-VALIDATE Rows=1 Packs=1 Skills=1 Errors=0 Warnings=1"], PacksRunner.Lines(output));
    }

    /// <summary>A missing SKILLS.md is R001 and exit 1, not exit 2.</summary>
    [Fact]
    public void Validate_NoSkillsMd_R001Exit1()
    {
        RegistryFixture fixture = new();
        PacksRunner.Ready(fixture, HelpText.Version);
        fixture.Files.Remove(Path.Combine(fixture.Dir, "SKILLS.md"));

        (int exit, string output, _) = PacksRunner.RunRaw(fixture.Files, ["validate", "--data-dir", PacksRunner.DataDir]);

        Assert.Equal(1, exit);
        Assert.StartsWith("ERROR  R001  SKILLS.md", PacksRunner.Lines(output)[0], StringComparison.Ordinal);
    }

    /// <summary><c>--json</c> prints one object with the summary and lower-case severities.</summary>
    [Fact]
    public void Validate_Json_Shape()
    {
        RegistryFixture fixture = new();
        fixture.Source().Pack("documents", """[{ "name": "docx", "path": "" }]""", appliesTo: """["accountant"]""");

        (_, string output, _) = PacksRunner.Run(fixture, "validate", "--json");

        using JsonDocument document = JsonDocument.Parse(output);
        JsonElement root = document.RootElement;
        Assert.Equal(1, root.GetProperty("summary").GetProperty("rows").GetInt32());
        JsonElement first = root.GetProperty("findings")[0];
        Assert.Equal("error", first.GetProperty("severity").GetString());
        Assert.Equal("packs/documents.skillpack.json", first.GetProperty("file").GetString());
        Assert.DoesNotContain("PACKS-VALIDATE", output, StringComparison.Ordinal);
    }

    /// <summary>A path filter checks only the rows at or under it.</summary>
    [Fact]
    public void Validate_PathFilter_OnlyThoseRows()
    {
        RegistryFixture fixture = Healthy();
        fixture.PackRow(InMemoryFileReader.At("gone.skillpack.json"));

        (int exit, string output, _) = PacksRunner.Run(fixture, "validate", "packs/documents.skillpack.json");

        Assert.Equal(0, exit);
        Assert.Equal("PACKS-VALIDATE Rows=1 Packs=1 Skills=1 Errors=0 Warnings=0", PacksRunner.Lines(output)[^1]);
    }

    /// <summary>A pack file no row points at is checked as a virtual row and told to add a row.</summary>
    [Fact]
    public void Validate_FileNotInSkillsMd_VirtualRow()
    {
        RegistryFixture fixture = Healthy();
        string folder = fixture.CloneSkill("pdf");
        fixture.SkillFolder(folder);
        string loose = Path.Combine(fixture.Dir, "packs", "new.skillpack.json");
        fixture.Files.Add(loose, $$"""{ "schemaVersion": 1, "id": "new", "version": "1", "title": "New", "source": "local/test", "appliesTo": ["developer"], "consultWhenAdds": "x", "skills": [{ "name": "pdf", "path": "{{Json(folder)}}", "origin": "{{Pinned}}pdf" }] }""");

        (int exit, string output, _) = PacksRunner.Run(fixture, "validate", loose);

        Assert.Equal(0, exit);
        Assert.Contains("OK           packs/new.skillpack.json  packs/new.skillpack.json is not in SKILLS.md yet; add a row", PacksRunner.Lines(output));
    }

    /// <summary>A folder with a SKILL.md that no row reaches is a virtual Skill: Rows=0, Skills=1.</summary>
    [Fact]
    public void Validate_LooseSkillFolder_VirtualSkill()
    {
        RegistryFixture fixture = Healthy();
        string folder = InMemoryFileReader.At("Elsewhere", "pdf");
        fixture.SkillFolder(folder, frontmatterExtra: "tools: propose_sprint\n");

        (int exit, string output, _) = PacksRunner.Run(fixture, "validate", folder);

        Assert.Equal(1, exit);
        Assert.Equal("PACKS-VALIDATE Rows=0 Packs=0 Skills=1 Errors=1 Warnings=0", PacksRunner.Lines(output)[^1]);
    }

    /// <summary>A path that does not exist and matches nothing, or exists and matches nothing, is exit 2.</summary>
    [Fact]
    public void Validate_PathMatchesNothing_Exit2()
    {
        RegistryFixture missing = Healthy();
        RegistryFixture unrelated = Healthy();
        string other = InMemoryFileReader.At("Elsewhere", "notes.txt");
        unrelated.Files.Add(other, "x");

        (int firstExit, _, string firstError) = PacksRunner.Run(missing, "validate", InMemoryFileReader.At("nope"));
        (int secondExit, _, string secondError) = PacksRunner.Run(unrelated, "validate", other);

        Assert.Equal(2, firstExit);
        Assert.StartsWith("Path not found:", firstError, StringComparison.Ordinal);
        Assert.Equal(2, secondExit);
        Assert.StartsWith("No row in SKILLS.md points at or under", secondError, StringComparison.Ordinal);
    }

    /// <summary>A context written by another Huddle version adds X002 to every run.</summary>
    [Fact]
    public void Validate_VersionMismatch_X002()
    {
        RegistryFixture fixture = Healthy();
        PacksRunner.Ready(fixture, "9.9.9");

        (int exit, string output, _) = PacksRunner.RunRaw(fixture.Files, ["validate", "--data-dir", PacksRunner.DataDir]);

        Assert.Equal(0, exit);
        Assert.Contains(PacksRunner.Lines(output), static line => line.StartsWith("WARN   X002  SKILLS.md", StringComparison.Ordinal));
    }
}

/// <summary>Pins <c>roles</c>, <c>installed</c>, <c>skills</c> and <c>schema</c> (§6.4).</summary>
public sealed class ListCommandTests
{
    /// <summary><c>roles</c> prints one tab-separated line per role, sorted.</summary>
    [Fact]
    public void Roles_TabSeparatedSorted()
    {
        (int exit, string output, _) = PacksRunner.Run(Healthy(), "roles");

        Assert.Equal(0, exit);
        Assert.Equal(["bookkeeper\tBookkeeper", "developer\tDeveloper"], PacksRunner.Lines(output));
    }

    /// <summary><c>installed</c> names built-in Skills as built-in.</summary>
    [Fact]
    public void Installed_BuiltInOrigin()
    {
        RegistryFixture fixture = Healthy();
        fixture.Installed.Add(new InstalledSkill("read-skill", null, null, true));

        (_, string output, _) = PacksRunner.Run(fixture, "installed");

        Assert.Equal(["read-skill\tbuilt-in"], PacksRunner.Lines(output));
    }

    /// <summary><c>skills</c> lists each Skill once with its state and sources, filtered by text.</summary>
    [Fact]
    public void Skills_ListsAndFilters()
    {
        RegistryFixture fixture = Healthy();
        fixture.Catalog("c", $$"""[{ "name": "pdf", "description": "Reads PDF files.", "origin": "{{Pinned}}pdf", "path": "" }]""");

        (_, string all, _) = PacksRunner.Run(fixture, "skills");
        (_, string filtered, _) = PacksRunner.Run(fixture, "skills", "PDF");

        Assert.Equal(["docx\tdownloaded\tpack:documents\tDoes docx things.", "pdf\tnot-downloaded\tcatalog:c\tReads PDF files."], PacksRunner.Lines(all));
        Assert.Equal(["pdf\tnot-downloaded\tcatalog:c\tReads PDF files."], PacksRunner.Lines(filtered));
    }

    /// <summary><c>schema pack</c> prints the embedded schema and needs no data folder.</summary>
    [Fact]
    public void Schema_PrintsEmbeddedText()
    {
        (int exit, string output, _) = PacksRunner.RunRaw(new InMemoryFileReader(), ["schema", "pack"]);

        Assert.Equal(0, exit);
        Assert.Equal(SchemaFiles.Read("pack")?.TrimEnd(), output.TrimEnd());
    }
}

/// <summary>Pins principle 2: the command's findings equal <c>Checks.Run</c> on the same tree, and it writes nothing.</summary>
public sealed class ParityTests
{
    /// <summary>The JSON findings equal what the library returns for the same files and context.</summary>
    [Fact]
    public void Cli_Findings_EqualLibrary()
    {
        RegistryFixture fixture = Healthy();
        fixture.Roles.Add(new RoleInfo("reviewer", "Reviewer", "Reviews.", ["absent"]));
        fixture.Catalog("c", """[{ "name": "pdf", "description": "d", "origin": "https://github.com/o/r/tree/main/pdf", "path": "missing" }]""");
        PacksRunner.Ready(fixture, HelpText.Version);
        CheckResult library = CheckRunner.Run(fixture.Resolve(new ResolveOptions(IgnoreGrace: true)), fixture.Context(HelpText.Version), new CheckOptions(ToolVersion: HelpText.Version), fixture.Files, TestContext.Current.CancellationToken);
        int readsBefore = fixture.Files.Reads;

        (_, string output, _) = PacksRunner.RunRaw(fixture.Files, ["validate", "--json", "--data-dir", PacksRunner.DataDir]);

        using JsonDocument document = JsonDocument.Parse(output);
        string[] cli = [.. document.RootElement.GetProperty("findings").EnumerateArray().Select(static item => item.GetProperty("code").GetString() ?? string.Empty)];
        Assert.Equal(library.Findings.Select(static finding => finding.Code), cli);
        Assert.True(fixture.Files.Reads > readsBefore);
    }

    /// <summary>The app's build output holds the huddle-packs apphost beside the app (§8.9).</summary>
    [Fact]
    public void AppOutput_ContainsHuddlePacksApphost()
    {
        string folder = Path.GetDirectoryName(typeof(Agency.Huddle.App.TeamOptions).Assembly.Location) ?? string.Empty;
        string apphost = OperatingSystem.IsWindows() ? "huddle-packs.exe" : "huddle-packs";

        Assert.True(File.Exists(Path.Combine(folder, "huddle-packs.dll")));
        Assert.True(File.Exists(Path.Combine(folder, "huddle-packs.runtimeconfig.json")));
        Assert.True(File.Exists(Path.Combine(folder, apphost)), apphost);
    }
}
