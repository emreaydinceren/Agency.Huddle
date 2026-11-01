using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Agency.Huddle.Analyzers.Tests;

/// <summary>
/// Compiles one C# source against the running framework's assemblies and returns what an analyzer reports on it. A source
/// that does not compile is refused, because the compiler's errors would stop the analyzer from running at all.
/// </summary>
internal static class AnalyzerHarness
{
    /// <summary>
    /// The stand-ins a source pastes in to look like Huddle.App: the two helpers that switch the rules on, and
    /// <c>IHostedService</c>, which the test project does not reference.
    /// </summary>
    internal const string AppStubs = """
        namespace Agency.Huddle.App.Services
        {
            internal static class EventRaise
            {
            }

            internal sealed class StopSignal
            {
            }
        }

        namespace Microsoft.Extensions.Hosting
        {
            public interface IHostedService
            {
                System.Threading.Tasks.Task StartAsync(System.Threading.CancellationToken cancellationToken);

                System.Threading.Tasks.Task StopAsync(System.Threading.CancellationToken cancellationToken);
            }
        }
        """;

    private static readonly Lazy<MetadataReference[]> FrameworkReferences = new(LoadFrameworkReferences);

    /// <summary>Runs <paramref name="analyzer"/> over <paramref name="source"/> and returns its diagnostics, ordered by position.</summary>
    /// <param name="analyzer">The analyzer under test.</param>
    /// <param name="source">A complete C# file.</param>
    /// <param name="ct">The test's token.</param>
    /// <returns>The analyzer's diagnostics.</returns>
    internal static async Task<ImmutableArray<Diagnostic>> RunAsync(DiagnosticAnalyzer analyzer, string source, CancellationToken ct)
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            "Probe",
            [CSharpSyntaxTree.ParseText(source, cancellationToken: ct)],
            FrameworkReferences.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        Diagnostic[] errors = [.. compilation.GetDiagnostics(ct).Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];
        if (errors.Length > 0)
        {
            throw new InvalidOperationException("The probe source does not compile: " + string.Join("; ", errors.Select(static error => error.ToString())));
        }

        ImmutableArray<Diagnostic> diagnostics = await compilation.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync(ct);
        return [.. diagnostics.OrderBy(static diagnostic => diagnostic.Location.SourceSpan.Start)];
    }

    private static MetadataReference[] LoadFrameworkReferences()
    {
        string paths = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string
            ?? throw new InvalidOperationException("The runtime did not list its trusted platform assemblies.");
        return [.. paths.Split(Path.PathSeparator)
            .Where(static path => Path.GetFileName(path).StartsWith("System.", StringComparison.Ordinal)
                || string.Equals(Path.GetFileName(path), "netstandard.dll", StringComparison.Ordinal))
            .Select(static path => MetadataReference.CreateFromFile(path))];
    }
}
