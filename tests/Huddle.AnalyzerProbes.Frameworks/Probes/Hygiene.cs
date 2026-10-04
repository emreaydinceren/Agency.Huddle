using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Agency.Huddle.AnalyzerProbes.Frameworks;

// Each "// probe: <RuleId>" tag is answered by that rule firing on the next few lines.
// The file is meant to be wrong; do not copy anything from it.

[TestClass]
public class MsTestHygiene
{
    // probe: S1607
    [TestMethod]
    [Ignore]
    public void Ignored() { Assert.IsTrue(true); }

    // probe: S3433
    [TestMethod]
    private void NotPublic() { Assert.IsTrue(1 > 0); }
}

// probe: S2187
[TestClass]
public class MsTestNoTests
{
    public void NotATest() { }
}
