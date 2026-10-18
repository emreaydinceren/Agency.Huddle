using System.Text;
using System.Text.Json;
using Agency.Huddle.App;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.SkillSources;
using Agency.Huddle.SkillPacks;
using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.Tests.SkillPacks.Prompts;
using Agency.Huddle.Tests.SkillPacks.RegistryService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.SkillPacks.Install;

/// <summary>Pins <see cref="InstallFileWriter"/> (§6.7): both files at start, the front matter, the body from its prompt,
/// coalescing, no write when nothing changed, and registration after the watcher.</summary>
public sealed class InstallFileWriterTests
{
    /// <summary>A host with one Pack, read, and a writer over it.</summary>
    private static async Task<(RegistryHost Host, InstallFileWriter Writer)> Ready()
    {
        RegistryHost host = new();
        host.Source();
        host.Pack("documents", "docx", host.CloneSkill("docx"));
        host.SkillsMd(["packs/documents.skillpack.json"]);
        await host.Registry.ReloadAsync(TestContext.Current.CancellationToken);
        InstallFileWriter writer = new(host.Roles, host.Skills, host.Registry, new RecordingPromptSource(), host.Settings, NullLogger<InstallFileWriter>.Instance, host.Clock);
        return (host, writer);
    }

    /// <summary>Start writes both files: the document with the front matter keys in order, and the context file.</summary>
    [Fact]
    public async Task StartAsync_WritesBothFiles_FrontMatterInOrder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (RegistryHost host, InstallFileWriter writer) = await Ready();
        using RegistryHost owned = host;
        using InstallFileWriter disposing = writer;

        await writer.StartAsync(ct);

        string document = File.ReadAllText(writer.InstallFilePath);
        string[] keys = [.. document.Split('\n').Skip(1).TakeWhile(static line => line != "---").Select(static line => line[..line.IndexOf(':', StringComparison.Ordinal)])];
        Assert.Equal(["huddle", "schemaVersion", "dataDir", "skillSourcesDir", "registry", "context", "validate", "computer", "written"], keys);
        Assert.Contains("validate: '\"", document, StringComparison.Ordinal);
        Assert.Contains("computer: DESK-01\n", document, StringComparison.Ordinal);
        Assert.Contains("\n# InstallSkillPack.md\n\n## 1. What you are doing", document, StringComparison.Ordinal);
        Assert.Contains("## Appendix D. Where to look", document, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", document, StringComparison.Ordinal);
        InstallContextReadResult context = InstallContext.Parse(File.ReadAllText(writer.ContextFilePath));
        Assert.NotNull(context.Value);
        Assert.Equal("DESK-01", context.Value.Computer);
    }

    /// <summary>The body carries the generated hint table: every code of the check catalog, with its fix hint.</summary>
    [Fact]
    public async Task Body_CheckHints_FromCatalog()
    {
        (RegistryHost host, InstallFileWriter writer) = await Ready();
        using RegistryHost owned = host;
        using InstallFileWriter disposing = writer;

        (string document, _) = writer.Compose();

        Assert.All(CheckCatalog.All, info => Assert.Contains($"| `{info.Code}` |", document, StringComparison.Ordinal));
        Assert.Contains(InstallFileWriter.Cell(CheckCatalog.HintFor("S011"), int.MaxValue), document, StringComparison.Ordinal);
    }

    /// <summary>A <c>.dll</c> program is run through <c>dotnet</c>; an exe is run directly.</summary>
    [Fact]
    public async Task ValidateCommand_DotnetFallback()
    {
        (RegistryHost host, _) = await Ready();
        using RegistryHost owned = host;
        SkillSourcesSettings dll = new(Microsoft.Extensions.Options.Options.Create(new TeamOptions { DataDir = host.DataDir, HuddlePacksPath = @"C:\Huddle\huddle-packs.dll" }), NullLogger<SkillSourcesSettings>.Instance);
        using InstallFileWriter writer = new(host.Roles, host.Skills, host.Registry, new RecordingPromptSource(), dll, NullLogger<InstallFileWriter>.Instance, host.Clock);

        Assert.StartsWith("dotnet \"", writer.ValidateCommand, StringComparison.Ordinal);
        Assert.EndsWith($"validate --data-dir \"{host.DataDir}\"", writer.ValidateCommand, StringComparison.Ordinal);
    }

    /// <summary>A burst of change requests writes once after the second; a request with nothing changed writes nothing.</summary>
    [Fact]
    public async Task RequestWrite_Coalesces_AndSkipsWhenUnchanged()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (RegistryHost host, InstallFileWriter writer) = await Ready();
        using RegistryHost owned = host;
        using InstallFileWriter disposing = writer;
        await writer.StartAsync(ct);
        DateTime first = File.GetLastWriteTimeUtc(writer.InstallFilePath);
        await Task.Delay(30, ct);

        for (int i = 0; i < 5; i++)
        {
            writer.RequestWrite();
        }

        host.Clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(first, File.GetLastWriteTimeUtc(writer.InstallFilePath));

        host.Pack("sheets", "xlsx", host.CloneSkill("xlsx"));
        host.SkillsMd(["packs/documents.skillpack.json", "packs/sheets.skillpack.json"]);
        await host.Registry.RebuildAsync(Agency.Huddle.App.SkillSources.RebuildScope.All, ct);
        host.Clock.Advance(TimeSpan.FromSeconds(2));

        Assert.Contains("packs/sheets.skillpack.json".Replace('/', Path.DirectorySeparatorChar), File.ReadAllText(writer.InstallFilePath), StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(host.DataDir, "*.tmp"));
    }

    /// <summary>The writer is a hosted service registered right after the watcher.</summary>
    [Fact]
    public void AddTeamServices_WriterAfterWatcher()
    {
        using TempDataDir dataDir = new();
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Team:DataDir"] = dataDir.Path }).Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddTeamServices(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();

        List<IHostedService> hosted = [.. provider.GetServices<IHostedService>()];

        Assert.Equal(hosted.FindIndex(static service => service is RegistryWatcher) + 1, hosted.FindIndex(static service => service is InstallFileWriter));
    }
}

/// <summary>Pins the four appendices (§6.7, "The appendices"): order, caps and their exact lines, and cell escaping.</summary>
public sealed class InstallAppendixTests
{
    private static readonly string[] Headings = ["## 11. Telling the Human", "## Appendix A. Roles", "## Appendix B. Installed Skills", "## Appendix C. SKILLS.md today", "## Appendix D. Where to look"];

    /// <summary>A cell escapes a bar, collapses white space and cuts a long description.</summary>
    [Fact]
    public void Cell_EscapesAndCuts()
    {
        Assert.Equal("a \\| b c", InstallFileWriter.Cell("a | b\n  c"));
        Assert.EndsWith("…", InstallFileWriter.Cell(new string('x', 200)), StringComparison.Ordinal);
        Assert.Equal(160, InstallFileWriter.Cell(new string('x', 200)).Length);
        Assert.Equal("`ab`", InstallFileWriter.Code("a`b"));
    }

    /// <summary>The four appendix headings come after the body, in order.</summary>
    [Fact]
    public async Task Appendices_InOrder()
    {
        using RegistryHost host = new();
        await host.Registry.ReloadAsync(TestContext.Current.CancellationToken);
        using InstallFileWriter writer = new(host.Roles, host.Skills, host.Registry, new RecordingPromptSource(), host.Settings, NullLogger<InstallFileWriter>.Instance, host.Clock);

        (string document, _) = writer.Compose();

        int[] positions = [.. Headings.Select(heading => document.IndexOf(heading, StringComparison.Ordinal))];
        Assert.All(positions, static position => Assert.True(position >= 0));
        Assert.Equal(positions.Order(), positions);
        Assert.Contains("| `team-building` | built in | yes |", document, StringComparison.Ordinal);
    }

    /// <summary>The YAML rule quotes what needs quoting and doubles a single quote.</summary>
    [Fact]
    public void YamlScalar_QuotesWhenNeeded()
    {
        Assert.Equal(@"E:\Huddle", InstallFileWriter.YamlScalar(@"E:\Huddle"));
        Assert.Equal("'\"C:\\x.exe\" validate'", InstallFileWriter.YamlScalar("\"C:\\x.exe\" validate"));
        Assert.Equal("'it''s: here'", InstallFileWriter.YamlScalar("it's: here"));
        Assert.Equal("''", InstallFileWriter.YamlScalar(string.Empty));
    }
}

/// <summary>Pins <c>install-context.json</c> as the writer writes it (§6.7, §7.2): every field, uncapped, suggestions, nulls for built-ins, tools sorted.</summary>
public sealed class InstallContextFileTests
{
    /// <summary>The context file holds every field, suggested Packs, null origin for built-ins, and sorted App Tools.</summary>
    [Fact]
    public async Task ContextFile_HasEveryField()
    {
        using RegistryHost host = new();
        await host.Registry.ReloadAsync(TestContext.Current.CancellationToken);
        using InstallFileWriter writer = new(host.Roles, host.Skills, host.Registry, new RecordingPromptSource(), host.Settings, NullLogger<InstallFileWriter>.Instance, host.Clock);

        (_, string json) = writer.Compose();

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.Equal(["schemaVersion", "huddleVersion", "computer", "dataDir", "skillSourcesDir", "skillsDir", "roles", "installed", "appTools", "written"], root.EnumerateObject().Select(static property => property.Name));
        Assert.All(root.GetProperty("roles").EnumerateArray(), static role => Assert.True(role.TryGetProperty("suggestedPacks", out _)));
        JsonElement builtIn = root.GetProperty("installed").EnumerateArray().First(static skill => skill.GetProperty("builtIn").GetBoolean());
        Assert.Equal(JsonValueKind.Null, builtIn.GetProperty("origin").ValueKind);
        string[] tools = [.. root.GetProperty("appTools").EnumerateArray().Select(static tool => tool.GetString() ?? string.Empty)];
        Assert.Equal(tools.Order(StringComparer.Ordinal), tools);
    }
}

/// <summary>Pins the default <c>installSkillPack.body</c> (§6.7, Appendix C): eleven sections and the two size limits.</summary>
public sealed class InstallBodyDefaultTests
{
    /// <summary>The raw default has eleven numbered sections and is at most 9,600 bytes.</summary>
    [Fact]
    public void Default_ElevenSections_AtMost9600Bytes()
    {
        string body = PromptCatalog.All.Single(static definition => definition.Key == "installSkillPack.body").Default;

        Assert.Equal(11, body.Split('\n').Count(static line => line.StartsWith("## ", StringComparison.Ordinal)));
        Assert.Contains("## 11. Telling the Human", body, StringComparison.Ordinal);
        Assert.InRange(Encoding.UTF8.GetByteCount(body), 1, 9600);
    }

    /// <summary>The rendered fixed part, with the full hint table and 260-character paths, is at most 20,480 bytes.</summary>
    [Fact]
    public void Rendered_FixedPart_AtMost20480Bytes()
    {
        string longPath = @"C:\" + new string('p', 256);
        Dictionary<string, string> values = new(StringComparer.Ordinal)
        {
            ["{{computer}}"] = "DESK-01",
            ["{{skillSourcesDir}}"] = longPath,
            ["{{registryPath}}"] = longPath,
            ["{{validateCommand}}"] = $"\"{longPath}\" validate --data-dir \"{longPath}\"",
            ["{{checkHints}}"] = InstallFileWriter.CheckHintsTable(),
        };

        string rendered = new RecordingPromptSource().Render("installSkillPack.body", values);

        Assert.InRange(Encoding.UTF8.GetByteCount(rendered), 1, 20480);
    }
}
