using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Agency.Huddle.Analyzers;

/// <summary>
/// HUD0002: a type that implements <c>IHostedService</c> (which includes <c>BackgroundService</c> and
/// <c>IHostedLifecycleService</c>) holds a <see cref="System.Threading.CancellationTokenSource"/> in an instance field or
/// property. Four such services went red in CI in two days with the same Dispose-versus-StopAsync race; each now holds a
/// <c>StopSignal</c>. A token source local to one method is not a field and is not reported.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HostedServiceStopSignalAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The helper whose presence in the compilation switches the rule on.</summary>
    internal const string HelperMetadataName = "Agency.Huddle.App.Services.StopSignal";

    private const string HostedServiceMetadataName = "Microsoft.Extensions.Hosting.IHostedService";
    private const string TokenSourceMetadataName = "System.Threading.CancellationTokenSource";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(HuddleDiagnostics.HostedServiceTokenSource);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            if (!OwnHelper.IsDeclared(start.Compilation, HelperMetadataName)
                || start.Compilation.GetTypeByMetadataName(HostedServiceMetadataName) is not { } hostedService
                || start.Compilation.GetTypeByMetadataName(TokenSourceMetadataName) is not { } tokenSource)
            {
                return;
            }

            start.RegisterSymbolAction(
                symbolContext => Analyze(symbolContext, hostedService, tokenSource),
                SymbolKind.Field,
                SymbolKind.Property);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol hostedService, INamedTypeSymbol tokenSource)
    {
        ISymbol member = context.Symbol;
        if (member.IsStatic || member.IsImplicitlyDeclared)
        {
            return;
        }

        ITypeSymbol? memberType = member switch
        {
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            _ => null,
        };

        INamedTypeSymbol owner = member.ContainingType;
        if (memberType is null
            || !IsOrDerivesFrom(memberType, tokenSource)
            || !owner.AllInterfaces.Contains(hostedService, SymbolEqualityComparer.Default))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(HuddleDiagnostics.HostedServiceTokenSource, member.Locations[0], owner.Name, member.Name));
    }

    private static bool IsOrDerivesFrom(ITypeSymbol type, INamedTypeSymbol baseType)
    {
        for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }
}
