using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Agency.Huddle.Analyzers.Tests;

public sealed class EventRaiseAnalyzerTests
{
    private const string Raiser = """
        namespace Probe
        {
            internal sealed class Store
            {
                public event System.Action? Changed;

                public event System.Action<int>? Counted;

                public void Save(System.Func<int>? read, System.Action? callback)
                {
                    RAISE
                }
            }
        }
        """;

    /// <summary>Each way of invoking an event's delegate directly is reported once, naming the event.</summary>
    [Theory]
    [InlineData("this.Changed?.Invoke();", "Changed")]
    [InlineData("if (this.Changed is not null) { this.Changed.Invoke(); }", "Changed")]
    [InlineData("if (this.Changed is not null) { this.Changed(); }", "Changed")]
    [InlineData("this.Counted?.Invoke(1);", "Counted")]
    public async Task DirectRaise_IsReported(string raise, string eventName)
    {
        ImmutableArray<Diagnostic> diagnostics = await RunAsync(raise, withHelper: true);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HUD0001", diagnostic.Id);
        Assert.Equal(
            $"Event '{eventName}' is raised directly; raise it through EventRaise.Isolated so a subscriber that throws neither skips the others nor fails the raiser",
            diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>A delegate that is not an event (a Func result, a callback parameter) is control flow and is not reported.</summary>
    [Theory]
    [InlineData("_ = read?.Invoke();")]
    [InlineData("callback?.Invoke();")]
    [InlineData("_ = this.Changed?.GetInvocationList();")]
    public async Task NonEventDelegate_IsNotReported(string call)
    {
        ImmutableArray<Diagnostic> diagnostics = await RunAsync(call, withHelper: true);

        Assert.Empty(diagnostics);
    }

    /// <summary>Without EventRaise declared in the assembly (the ACP projects, test projects) the rule stays silent.</summary>
    [Fact]
    public async Task HelperNotDeclared_IsNotReported()
    {
        ImmutableArray<Diagnostic> diagnostics = await RunAsync("this.Changed?.Invoke();", withHelper: false);

        Assert.Empty(diagnostics);
    }

    private static Task<ImmutableArray<Diagnostic>> RunAsync(string raise, bool withHelper)
    {
        string source = Raiser.Replace("RAISE", raise, StringComparison.Ordinal) + (withHelper ? AnalyzerHarness.AppStubs : string.Empty);
        return AnalyzerHarness.RunAsync(new EventRaiseAnalyzer(), source, TestContext.Current.CancellationToken);
    }
}
