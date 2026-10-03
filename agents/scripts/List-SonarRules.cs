#:package Microsoft.CodeAnalysis.CSharp@4.12.0
#:property ManagePackageVersionsCentrally=false
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property GenerateDocumentationFile=false
#:property NoWarn=IL2026;IL2072
// Prints every rule the pinned SonarAnalyzer.CSharp ships: id, default severity, whether it is on by
// default, and its title, tab separated, sorted by id. The titles are what lets a rule be judged on
// what it says instead of on its number. Run it from the repository root:
//
//     dotnet run agents/scripts/List-SonarRules.cs > sonar-rules.tsv
//
// The #:property lines opt it out of the repository's central package management and strict build
// settings, which would otherwise reject a standalone script. It needs the Sonar package restored once
// (any build of the solution does that). SonarVersion below must match Directory.Packages.props;
// change both together.
using System.Reflection;
using Microsoft.CodeAnalysis.Diagnostics;

const string SonarVersion = "10.32.0.713";

string dll = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".nuget", "packages", "sonaranalyzer.csharp", SonarVersion, "analyzers", "SonarAnalyzer.CSharp.dll");
if (!File.Exists(dll))
{
    Console.Error.WriteLine($"Not found: {dll}. Build the solution once to restore the package, or fix SonarVersion.");
    return 1;
}

Assembly assembly = Assembly.LoadFrom(dll);
Type[] types;
try
{
    types = assembly.GetTypes();
}
catch (ReflectionTypeLoadException ex)
{
    types = ex.Types.OfType<Type>().ToArray();
}

SortedDictionary<string, string> rows = new(StringComparer.Ordinal);
foreach (Type type in types)
{
    if (type.IsAbstract || !type.IsSubclassOf(typeof(DiagnosticAnalyzer)))
    {
        continue;
    }

    object? instance;
    try
    {
        instance = Activator.CreateInstance(type);
    }
    catch (Exception ex) when (ex is MissingMethodException or TargetInvocationException)
    {
        continue;
    }

    if (instance is not DiagnosticAnalyzer analyzer)
    {
        continue;
    }

    foreach (var descriptor in analyzer.SupportedDiagnostics)
    {
        rows[descriptor.Id] = $"{descriptor.Id}\t{descriptor.DefaultSeverity}\t{descriptor.IsEnabledByDefault}\t{descriptor.Title}";
    }
}

Console.WriteLine("Id\tSeverity\tOnByDefault\tTitle");
foreach (string row in rows.Values)
{
    Console.WriteLine(row);
}

return 0;
