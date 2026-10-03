using Xunit;

namespace Agency.Huddle.AnalyzerProbes.Tests;

// Each "// probe: <RuleId>" tag is answered by that rule firing on the next few lines.
// The file is meant to be wrong; do not copy anything from it.

public sealed class TestHygiene
{
    // probe: S2699
    /// <summary>A test with no assertion, which S2699 must flag.</summary>
    [Fact]
    public void NoAssertion() { int x = 1; x++; }

    /// <summary>A test asserting a literal boolean, which S2701 must flag.</summary>
    [Fact]
    public void LiteralAssertion() { bool flag = Sleeps2(); /* probe: S2701 */ Assert.Equal(true, flag); }

    private static bool Sleeps2() => true;

    /// <summary>A test that sleeps, which S2925 must flag.</summary>
    [Fact]
    public void Sleeps() { /* probe: S2925 */ Thread.Sleep(10); Assert.True(1 > 0); }

    /// <summary>A test with swapped assertion arguments, which S3415 must flag.</summary>
    [Fact]
    public void Swapped() { int actual = 3; /* probe: S3415 */ Assert.Equal(actual, 5); }

    /// <summary>A test with a bad signature, kept to show S3433 does not fire on it.</summary>
    [Fact]
    public static int Returns() { Assert.True(1 > 0); return 1; }

    /// <summary>An async void test, kept to show S3433 does not fire on it.</summary>
    [Fact]
    public async void AsyncVoid() { await Task.Delay(1); Assert.True(1 > 0); }
}

public sealed class EmptyTests
{
    public void NotATest() { }
}
