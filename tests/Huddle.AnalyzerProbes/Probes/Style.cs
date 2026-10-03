using System.Diagnostics;
using System.Text;

namespace Agency.Huddle.AnalyzerProbes;

// Each "// probe: <RuleId>" tag is answered by that rule firing on the next few lines.
// The file is meant to be wrong; do not copy anything from it.

public class StyleA
{
    private int field;

    [Obsolete] // probe: S1123
    public void ObsoleteNoReason() { }

    [Obsolete("use something else")] // probe: S1133
    public void ObsoleteWithReason() { }

    // probe: S1134
    // FIXME remove this
    public void Marker() { }

    public void ThrowInFinally()
    {
        try { Marker(); }
        finally
        {
            // probe: S1163
            throw new InvalidOperationException();
        }
    }

    public int[] NullArray() { /* probe: S1168 */ return null; }

    public void Empty() { } // probe: S1186

    public void NestedBlock() { { Marker(); } } // probe: S1199

    public void WhileFor() { int i = 0; /* probe: S1264 */ for (; i < 3;) { i++; } }

    public void Discard() { /* probe: S1848 */ new StringBuilder(); }

    public bool TypeEq(object o) { /* probe: S2219 */ return o.GetType() == typeof(string); }

    public void Sub(int minuend, int subtrahend) { }

    public void CallSwapped(int minuend, int subtrahend) { /* probe: S2234 */ Sub(subtrahend, minuend); }

    public int SumUnchecked(IEnumerable<int> source) { /* probe: S2291 */ return unchecked(source.Sum()); }

    public int AsyncName() { /* probe: S2306 */ int async = 1; return async; }

    public void NoBraces(bool a)
    {
        // probe: S2681
        if (a)
            Marker();
            Marker();
    }

    public bool UnconstrainedNull<T>(T value) { /* probe: S2955 */ return value == null; }

    public string BuilderUnused() { /* probe: S3063 */ StringBuilder sb = new(); sb.Append("x"); return string.Empty; }

    public void Foreach(List<PublicBase> items) { /* probe: S3217 */ foreach (Reduced s in items) { Marker(); } }

    public void Casts(object o) { /* probe: S3247 */ if (o is string) { string s = (string)o; Marker(); } }

    public int Constant() { /* probe: S3400 */ return 42; }

    public void ToChars(string s) { /* probe: S3456 */ foreach (char c in s.ToCharArray()) { Marker(); } }

    public string FormatMismatch() { /* probe: S3457 */ return string.Format("{0} {1}", "a", "b", "c"); }

    public int Unassigned() { /* probe: S3459 */ return unassignedField; }
    private int unassignedField;

    public bool Else(int a, int b) { /* probe: S3972 */ if (a > 0) { Marker(); } if (b > 0) { Marker(); } return true; }

    public void Indent(bool a)
    {
        // probe: S3973
        if (a)
        Marker();
    }

    public void Dict(Dictionary<string, int> d) { /* probe: S4143 */ d["k"] = 1; d["k"] = 2; }

    // probe: S4136
    public void Overload(int a) { }
    public void Other() { }
    public void Overload(string a) { }

    // probe: S4663
    /**/
    public void EmptyComment() { }

    public void Trace1() { /* probe: S6670 */ Trace.Write("x"); }

    public void Trace2(TraceSwitch sw) { /* probe: S6675 */ Trace.WriteLineIf(sw.TraceInfo, "x"); }

    public async Task Twice(ValueTask<int> task)
    {
        /* probe: S5034 */
        await task;
        await task;
    }
}

// probe: S1694
public abstract class AllAbstract { public abstract void A(); public abstract void B(); }

// probe: S1939
public class RedundantBase : object { }

// probe: S3246
public interface IGet<T> { T Get(); }

// probe: S3260
public class Outer { private class NotSealed { } }

public interface IFirst { void Same(); }
public interface ISecond { void Same(); }
// probe: S3444
public interface IBoth : IFirst, ISecond { }

public class PublicBase { public virtual void M() { } public virtual void Hidden() { } }

public class Reduced : PublicBase
{
    private new void M() { }

    public new void Hidden() { }
}

public class StyleEvents
{
    // probe: S4220
    public event EventHandler? Changed;
    public void Raise() { Changed?.Invoke(null, EventArgs.Empty); }
}

public class DerivedParams
{
    public virtual void P(int[] a) { }
}

public partial class Partial
{
    // probe: S3251
    partial void NotImplemented();
}
