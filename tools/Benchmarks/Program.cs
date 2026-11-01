using BenchmarkDotNet.Running;

namespace Agency.Huddle.Tools.Benchmarks;

/// <summary>Runs the benchmark classes picked by <c>--filter</c>.</summary>
internal static class Program
{
    /// <summary>Hands the command line to BenchmarkDotNet's switcher.</summary>
    private static void Main(string[] args) => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
