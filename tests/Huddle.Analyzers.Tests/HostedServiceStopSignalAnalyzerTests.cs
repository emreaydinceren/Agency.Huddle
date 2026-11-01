using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Agency.Huddle.Analyzers.Tests;

public sealed class HostedServiceStopSignalAnalyzerTests
{
    private const string Service = """
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.Extensions.Hosting;

        namespace Probe
        {
            internal sealed class LinkedSource : CancellationTokenSource
            {
            }

            internal sealed class Service : BASES
            {
                MEMBER

                public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

                public Task StopAsync(CancellationToken cancellationToken)
                {
                    using CancellationTokenSource local = new();
                    return Task.CompletedTask;
                }
            }
        }
        """;

    /// <summary>A hosted service's token-source field or property, including a derived type, is reported once, naming both.</summary>
    [Theory]
    [InlineData("private readonly CancellationTokenSource stopping = new();", "stopping")]
    [InlineData("private CancellationTokenSource? current;", "current")]
    [InlineData("private readonly LinkedSource linked = new();", "linked")]
    [InlineData("public CancellationTokenSource Lifetime { get; } = new();", "Lifetime")]
    public async Task TokenSourceMember_InHostedService_IsReported(string member, string memberName)
    {
        ImmutableArray<Diagnostic> diagnostics = await RunAsync("IHostedService", member, withHelper: true);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HUD0002", diagnostic.Id);
        Assert.Equal(
            $"Hosted service 'Service' holds CancellationTokenSource '{memberName}'; use StopSignal, because the host can stop and dispose it on two threads at once",
            diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>A token source local to one method, a static one, and a StopSignal field are all left alone.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("private static readonly CancellationTokenSource Shared = new();")]
    [InlineData("private readonly Agency.Huddle.App.Services.StopSignal stopping = new();")]
    public async Task AllowedMembers_AreNotReported(string member)
    {
        ImmutableArray<Diagnostic> diagnostics = await RunAsync("IHostedService", member, withHelper: true);

        Assert.Empty(diagnostics);
    }

    /// <summary>A type that is not a hosted service may keep a token-source field.</summary>
    [Fact]
    public async Task NotHostedService_IsNotReported()
    {
        ImmutableArray<Diagnostic> diagnostics = await RunAsync("System.IDisposable", "private readonly CancellationTokenSource stopping = new(); public void Dispose() => this.stopping.Dispose();", withHelper: true);

        Assert.Empty(diagnostics);
    }

    /// <summary>Without StopSignal declared in the assembly the rule stays silent, so the fix is always within reach.</summary>
    [Fact]
    public async Task HelperNotDeclared_IsNotReported()
    {
        ImmutableArray<Diagnostic> diagnostics = await RunAsync("IHostedService", "private readonly CancellationTokenSource stopping = new();", withHelper: false);

        Assert.Empty(diagnostics);
    }

    private static Task<ImmutableArray<Diagnostic>> RunAsync(string bases, string member, bool withHelper)
    {
        string stubs = withHelper ? AnalyzerHarness.AppStubs : HostingOnly;
        string source = Service
            .Replace("BASES", bases, StringComparison.Ordinal)
            .Replace("MEMBER", member, StringComparison.Ordinal) + stubs;
        return AnalyzerHarness.RunAsync(new HostedServiceStopSignalAnalyzer(), source, TestContext.Current.CancellationToken);
    }

    private const string HostingOnly = """
        namespace Microsoft.Extensions.Hosting
        {
            public interface IHostedService
            {
                System.Threading.Tasks.Task StartAsync(System.Threading.CancellationToken cancellationToken);

                System.Threading.Tasks.Task StopAsync(System.Threading.CancellationToken cancellationToken);
            }
        }
        """;
}
