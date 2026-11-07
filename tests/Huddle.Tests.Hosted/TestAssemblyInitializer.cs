using System.Runtime.CompilerServices;

namespace Agency.Huddle.Tests;

/// <summary>Runs the shared test-process setup once, when this test assembly loads.</summary>
internal static class TestAssemblyInitializer
{
    /// <summary>Applies the <c>HUDDLE_TEST_TEMP</c> redirect before the first test touches the temp folder, and raises the thread pool's minimum before the first test blocks a thread.</summary>
    [ModuleInitializer]
    internal static void Initialize()
    {
        TestTempRedirect.Apply();
        TestThreadPool.Raise();
    }
}
