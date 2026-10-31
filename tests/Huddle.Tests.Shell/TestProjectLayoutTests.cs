using System.Xml.Linq;

namespace Agency.Huddle.Tests;

/// <summary>
/// Pins the layout every chat-surface test project must follow: it is listed in <c>Huddle.slnx</c> and in
/// <c>tests/Huddle.TestAssemblies.props</c>, and it carries a <c>xunit.runner.json</c> and an
/// <c>*AssemblySetupTests.cs</c> file. A test project is a folder <c>tests/Huddle.Tests.*</c> whose csproj sets
/// <c>IsTestProject</c> to true or imports <c>Huddle.Tests.Feature.props</c>.
/// </summary>
public sealed class TestProjectLayoutTests
{
    private const string ProjectPrefix = "Huddle.Tests.";
    private const string FeatureProps = "Huddle.Tests.Feature.props";

    /// <summary>Every test project is listed in <c>Huddle.slnx</c> by its exact <c>tests/Name/Name.csproj</c> path.</summary>
    [Fact]
    public void EveryTestProject_IsInTheSolution()
    {
        XDocument solution = XDocument.Load(RepoPaths.Combine("Huddle.slnx"));
        HashSet<string> paths = solution.Descendants("Project")
            .Select(static project => (string?)project.Attribute("Path"))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        List<string> failures = [];
        foreach (string name in FindTestProjects())
        {
            if (!paths.Contains($"tests/{name}/{name}.csproj"))
            {
                failures.Add(name);
            }
        }

        Assert.Empty(failures);
    }

    /// <summary>Every test project has an <c>InternalsVisibleTo</c> entry in <c>tests/Huddle.TestAssemblies.props</c>.</summary>
    [Fact]
    public void EveryTestProject_IsInTestAssembliesProps()
    {
        HashSet<string> visibleTo = LoadInternalsVisibleTo();

        List<string> failures = [];
        foreach (string name in FindTestProjects())
        {
            if (!visibleTo.Contains(name))
            {
                failures.Add(name);
            }
        }

        Assert.Empty(failures);
    }

    /// <summary>Every test project has a <c>xunit.runner.json</c> that sets <c>maxParallelThreads</c>.</summary>
    [Fact]
    public void EveryTestProject_HasARunnerConfig()
    {
        List<string> failures = [];
        foreach (string name in FindTestProjects())
        {
            string runner = RepoPaths.Combine("tests", name, "xunit.runner.json");
            if (!File.Exists(runner) || !File.ReadAllText(runner).Contains("maxParallelThreads", StringComparison.Ordinal))
            {
                failures.Add(name);
            }
        }

        Assert.Empty(failures);
    }

    /// <summary>Every test project has an <c>*AssemblySetupTests.cs</c> file pinning its shared test-process setup.</summary>
    [Fact]
    public void EveryTestProject_HasAnAssemblySetupTest()
    {
        List<string> failures = [];
        foreach (string name in FindTestProjects())
        {
            string folder = RepoPaths.Combine("tests", name);
            if (Directory.GetFiles(folder, "*AssemblySetupTests.cs", SearchOption.TopDirectoryOnly).Length == 0)
            {
                failures.Add(name);
            }
        }

        Assert.Empty(failures);
    }

    /// <summary>Every <c>InternalsVisibleTo</c> in <c>tests/Huddle.TestAssemblies.props</c> names a test folder that exists.</summary>
    [Fact]
    public void TestAssembliesProps_NamesOnlyExistingProjects()
    {
        List<string> failures = [];
        foreach (string name in LoadInternalsVisibleTo())
        {
            if (!Directory.Exists(RepoPaths.Combine("tests", name)))
            {
                failures.Add(name);
            }
        }

        Assert.Empty(failures);
    }

    private static List<string> FindTestProjects()
    {
        List<string> names = [];
        foreach (string folder in Directory.GetDirectories(RepoPaths.Combine("tests"), ProjectPrefix + "*"))
        {
            string name = Path.GetFileName(folder);
            string csproj = Path.Combine(folder, name + ".csproj");
            if (!File.Exists(csproj))
            {
                continue;
            }

            string text = File.ReadAllText(csproj);
            if (text.Contains("<IsTestProject>true</IsTestProject>", StringComparison.Ordinal)
                || text.Contains(FeatureProps, StringComparison.Ordinal))
            {
                names.Add(name);
            }
        }

        names.Sort(StringComparer.Ordinal);
        return names;
    }

    private static HashSet<string> LoadInternalsVisibleTo()
    {
        string path = RepoPaths.Combine("tests", "Huddle.TestAssemblies.props");
        Assert.True(File.Exists(path), $"Missing {path}: the shared list of test assemblies does not exist.");

        return XDocument.Load(path).Descendants("InternalsVisibleTo")
            .Select(static item => (string?)item.Attribute("Include"))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
    }
}
