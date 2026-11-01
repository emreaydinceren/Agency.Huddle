using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Agency.Huddle.Analyzers;

/// <summary>
/// HUD0001: an event's delegate is invoked directly (<c>this.Changed?.Invoke()</c>, <c>this.Changed.Invoke()</c> or
/// <c>this.Changed()</c>). Ninety such raises in Huddle.App were moved onto <c>EventRaise.Isolated</c>; this keeps the
/// next one from coming back. A delegate that is not an event (a <c>Func</c> result, a callback parameter) is control flow
/// and is not reported. A raise through a local copy of the event is not tracked.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EventRaiseAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The helper whose presence in the compilation switches the rule on.</summary>
    internal const string HelperMetadataName = "Agency.Huddle.App.Services.EventRaise";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(HuddleDiagnostics.EventRaisedDirectly);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        // Razor compiles @code blocks to generated files, and an event raised there is still an event.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            if (OwnHelper.IsDeclared(start.Compilation, HelperMetadataName))
            {
                start.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
            }
        });
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (invocation.TargetMethod.MethodKind != MethodKind.DelegateInvoke)
        {
            return;
        }

        if (RaisedEvent(invocation.Instance) is { } raised)
        {
            context.ReportDiagnostic(Diagnostic.Create(HuddleDiagnostics.EventRaisedDirectly, invocation.Syntax.GetLocation(), raised.Name));
        }
    }

    /// <summary>The event an invocation's receiver refers to, through a <c>?.</c> when there is one.</summary>
    private static IEventSymbol? RaisedEvent(IOperation? receiver) => receiver switch
    {
        IEventReferenceOperation reference => reference.Event,
        IConditionalAccessInstanceOperation conditional => ConditionalAccessTarget(conditional),
        _ => null,
    };

    /// <summary>The event the nearest enclosing <c>?.</c> tests, which is what the conditional instance stands for.</summary>
    private static IEventSymbol? ConditionalAccessTarget(IConditionalAccessInstanceOperation conditional)
    {
        for (IOperation? parent = conditional.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is IConditionalAccessOperation access)
            {
                return (access.Operation as IEventReferenceOperation)?.Event;
            }
        }

        return null;
    }
}
