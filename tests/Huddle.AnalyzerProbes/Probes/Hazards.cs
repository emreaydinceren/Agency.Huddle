using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Agency.Huddle.AnalyzerProbes;

// Each "// probe: <RuleId>" tag is answered by that rule firing on the next few lines.
// The file is meant to be wrong; do not copy anything from it.

// probe: S101
public sealed class bad_name { }

public sealed class HazardsA
{
    private static int staticCounter;
    private int plain;
    private readonly object gate = new();
    private object reassignable = new();

    public void Throws() { /* probe: S112 */ throw new Exception("x"); }

    public void ForInvariant() { /* probe: S127 */ for (int i = 0; i < 10; i++) { i += 2; } }

    public long Literal() { /* probe: S818 */ return 1l; }

    public void Jump()
    {
        // probe: S907
        goto done;
    done:
        return;
    }

    // probe: S1048
    ~HazardsA() { throw new InvalidOperationException(); }

    public void NestedIf(bool a, bool b)
    {
        // probe: S1066
        if (a) { if (b) { Throws(); } }
    }

    public Uri Hardcoded() => new Uri("http://example.org/path"); // probe: S1075

    public int Paren(int a) { /* probe: S1110 */ return ((a)); }

    public void EmptyStatement() { /* probe: S1116 */ ; }

    public void Shadow() { /* probe: S1117 */ int plain = 1; }

    public bool BoolLiteral(bool b) { /* probe: S1125 */ return b == true; }

    public void CollectNow() { /* probe: S1215 */ GC.Collect(); }

    public bool FloatEq(double d) { /* probe: S1244 */ return d == 0.5; }

    public bool Ip() { /* probe: S1313 */ return "8.8.8.8".Length > 0; }

    public void UnusedLocal() { /* probe: S1481 */ int unused = 5; }

    public string Concat(string[] items)
    {
        string s = "";
        // probe: S1643
        foreach (string item in items) { s += item; }
        return s;
    }

    public void SelfAssign(int a) { /* probe: S1656 */ a = a; }

    public void CatchNre() { try { Throws(); } /* probe: S1696 */ catch (NullReferenceException) { } }

    public void OneIteration()
    {
        // probe: S1751
        for (int i = 0; i < 3; i++) { Throws(); break; }
    }

    public bool Same(int a) { /* probe: S1764 */ return a == a; }

    public int DeadStore()
    {
        // probe: S1854
        int x = plain;
        x = 2;
        return x;
    }

    public int SameCondition(int a)
    {
        // probe: S1862
        if (a == 1) { return 1; } else if (a == 1) { return 2; }
        return 0;
    }

    public int SameBranch(bool a)
    {
        int r;
        // probe: S1871
        if (a) { r = 1; Throws(); } else if (!a) { r = 1; Throws(); } else { r = 3; }
        return r;
    }

    public int Redundant(int i) { /* probe: S1905 */ return (int)i; }

    public bool Inverted(int a, int b) { /* probe: S1940 */ return !(a == b); }

    public void Recursive() { /* probe: S2190 */ Recursive(); }

    public double IntDivision() { /* probe: S2184 */ double d = 1 / 2; return d; }

    public int Shift(int i) { /* probe: S2183 */ return i << 32; }

    public void Rotating()
    {
        // probe: S2251
        for (int i = 0; i < 10; i--) { Throws(); }
    }

    public void NeverTrue()
    {
        // probe: S2252
        for (int i = 10; i < 5; i++) { Throws(); }
    }

    public void SideEffectFree(string s) { /* probe: S2201 */ s.Trim(); }

    public string FormatBad() { /* probe: S2275 */ return string.Format("{0} {1}", 1); }

    public int ReadIgnored(Stream stream, byte[] buffer) { /* probe: S2674 */ stream.Read(buffer, 0, 1); return 0; }

    public bool Nan(double d) { /* probe: S2688 */ return d == double.NaN; }

    public bool IndexOfPositive(string s) { /* probe: S2692 */ return s.IndexOf("x") > 0; }

    public void WriteStatic() { /* probe: S2696 */ staticCounter = 1; }

    public int BitOp(int x) { /* probe: S2437 */ return x | 0; }

    public void LockReassignable()
    {
        // probe: S2445
        lock (this.reassignable) { Throws(); }
    }

    public void LockOnThis()
    {
        // probe: S2551
        lock (this) { Throws(); }
    }

    public void CatchGeneric() { try { Throws(); } /* probe: S2486 */ catch (Exception) { } }

    public void Rethrow() { try { Throws(); } /* probe: S2737 */ catch (Exception) { throw; } }

    public void RethrowEx() { try { Throws(); } catch (Exception ex) { /* probe: S3445 */ throw ex; } }

    public int Reverse(int x) { /* probe: S2757 */ int y = 1; y =+ x; return y; }

    public bool Doubled(bool b) { /* probe: S2761 */ return !!b; }

    public int Increment(int i) { /* probe: S2123 */ i = i++; return i; }

    public int Distinct(IEnumerable<int> source) { /* probe: S2971 */ return source.ToList().Count(); }

    public bool RefEq() { /* probe: S2995 */ return object.ReferenceEquals(1, 1); }

    public bool ThisIs() { /* probe: S3060 */ return this is HazardsA; }

    public void ReflectionHack(Type t) { /* probe: S3011 */ t.GetField("x", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); }

    public IEnumerable<int> TwoSorts(IEnumerable<int> source) { /* probe: S3169 */ return source.OrderBy(x => x).OrderBy(x => -x); }

    public void SleepyDebug(List<int> items) { /* probe: S3346 */ Debug.Assert(items.Remove(1)); }

    public int Nested(bool a, bool b) { /* probe: S3358 */ return a ? 1 : b ? 2 : 3; }

    public void CheckThenSet(int x) { /* probe: S3440 */ if (x != 1) { x = 1; } }

    public Type TypeOfType() { /* probe: S3443 */ return typeof(string).GetType(); }

    public void RedundantJump(bool a) { if (a) { Throws(); } /* probe: S3626 */ return; }

    public Exception? Unthrown() { /* probe: S3984 */ new InvalidOperationException(); return null; }

    public string Cases(int v)
    {
        switch (v)
        {
            case 1:
            default:
                return "x";
        }
    }

    public string DefaultMiddle(int v)
    {
        switch (v)
        {
            case 1: return "a";
            // probe: S4524
            default: return "b";
            case 2: return "c";
        }
    }

    public int BranchesSame(bool a) { /* probe: S3923 */ if (a) { return 1; } else { return 1; } }

    public void ArgName(int a) { /* probe: S3928 */ throw new ArgumentException("bad", "wrong"); }

    public bool EmptySize(List<int> l) { /* probe: S3981 */ return l.Count < 0; }

    public void NullThenIs(object o) { /* probe: S4201 */ if (o != null && o is string) { Throws(); } }

    public Guid Empty() { /* probe: S4581 */ return new Guid(); }

    public int StartIdx(string s) { /* probe: S4635 */ return s.Substring(1).IndexOf('x'); }

    public int RegexBad() { /* probe: S5856 */ return new System.Text.RegularExpressions.Regex("(", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1)).GetHashCode(); }

    public object RegexNoTimeout() { /* probe: S6444 */ return new System.Text.RegularExpressions.Regex("a"); }

    public DateTime Now() { /* probe: S6354 */ return DateTime.Now; }

    public TimeSpan Timing() { /* probe: S6561 */ DateTime start = DateTime.Now; return DateTime.Now - start; }

    public DateTime NoKind() { /* probe: S6562 */ return new DateTime(2020, 1, 1); }

    public DateTime Parse(string s) { /* probe: S6580 */ return DateTime.Parse(s); }

    public DateTime Epoch() { /* probe: S6588 */ return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc); }

    public IEnumerable<int> SortThenFilter(IEnumerable<int> source) { /* probe: S6607 */ return source.OrderBy(x => x).Where(x => x > 1); }

    public int FirstOfList(List<int> l) { /* probe: S6608 */ return l.First(); }

    public int MaxOfSet(SortedSet<int> set) { /* probe: S6609 */ return set.Max(); }

    public bool StartsWithText(string s) { /* probe: S6610 */ return s.StartsWith("a"); }

    public int ConcurrentCapture(ConcurrentDictionary<string, int> d, string key) { /* probe: S6612 */ return d.GetOrAdd(key, k => key.Length); }

    public int LastOfLinked(LinkedList<int> l) { /* probe: S6613 */ return l.First(); }

    public bool AnyEq(List<int> l) { /* probe: S6617 */ return l.Any(x => x == 1); }

    public string Formattable(int x) { /* probe: S6618 */ return FormattableString.Invariant($"{x}"); }

    public unsafe int Pointer() { /* probe: S6640 */ int x = 1; int* p = &x; return *p; }

    public async Task Cancel(CancellationToken ct) { /* probe: S8949 */ await Task.Delay(1); }

    public int Blocking(Task<int> t) { /* probe: S4462 */ return t.Result; }

    public async void Fire() { /* probe: S3168 */ await Task.Delay(1); }

    public Task<int> NullTask() { /* probe: S4586 */ return null; }

    public int ListOnly(List<int> list) { /* probe: S1155 */ return list.Count() > 0 ? 1 : 0; }

    public bool SplitBoth(bool a, bool b) { /* probe: S2178 */ return a & b; }

    public int Mixed(int a) { /* probe: S1121 */ int b; int d = (b = a) + 1; return b + d; }
}

public sealed class HazardsB
{
    // probe: S1104
    public int PublicField;

    // probe: S2223
    public static int MutableStatic;

    // probe: S2386
    public static int[] SharedArray = new int[3];

    // probe: S3887
    public readonly List<int> ReadonlyMutable = new();

    // probe: S1144
    private void NeverCalled() { }

    // probe: S4487
    private int neverRead;

    public void Writer() { this.neverRead = 1; }

    private readonly object gate = new();

    // probe: S2933
    private int onlyCtor;

    public HazardsB() { this.onlyCtor = 1; }

    [ThreadStatic]
    // probe: S2996
    private static int threadInit = 1;

    // probe: S3005
    [ThreadStatic]
    private int threadInstance;

    // probe: S2292
    private int backing;
    public int Backing { get { return backing; } set { backing = value; } }

    // probe: S2376
    public int WriteOnly { set { this.backing = value; } }

    // probe: S2372
    public int Throwing { get { throw new IOException(); } }

    // probe: S3237
    public int IgnoresValue { get => 1; set { } }

    // probe: S4275
    private int wrongField;
    private int other;
    public int WrongField { get { return this.other; } set { this.other = value; } }
}

public sealed class HazardsC
{
    public void Dispose() { } // probe: S2953

    // probe: S2368
    public void MultiDim(int[,] grid) { }

    // probe: S3010
    private static int created;
    public HazardsC() { created++; }

    public void Caller(int x, [CallerLineNumber] int line = 0) { }
    public void UseCaller() { /* probe: S3236 */ Caller(1, 5); }

    // probe: S3343
    public void CallerFirst([CallerMemberName] string name = "", int other = 0) { }

    // probe: S3241
    private int Unused() { return 1; }
    public void CallUnused() { Unused(); }

    // probe: S3427
    public void Over(int a = 0) { }
    public void Over(int a = 0, string b = "") { }

    // probe: S3447
    public void OptionalRef([Optional] ref int x) { }

    // probe: S3450
    public void DefaultParam([DefaultParameterValue(1)] int x) { }

    // probe: S3451
    public void DefaultValueParam([Optional][DefaultValue(1)] int x) { }

    // probe: S3603
    [System.Diagnostics.Contracts.Pure]
    public void PureVoid() { }
}

// probe: S1118
public class UtilityWithCtor { public static void A() { } public static void B() { } }

// probe: S2326
public static class Unusedtype { public static void M<T>() { } }

[Flags]
public enum ZeroNamed { Zero = 0, A = 1 } // probe: S2346

[Flags]
// probe: S2345
public enum NotExplicit { A = 1, B, C = 4 }

// probe: S4070
[Flags]
public enum NotFlags { A = 1, B = 3, C = 4 }

public enum FooEnum { A } // probe: S2344

public enum Bits { A = 1, B = 2 }

public sealed class EnumBits { public Bits Both() { /* probe: S3265 */ return Bits.A | Bits.B; } }

public sealed class Pair { }

public abstract class AbstractCtor { public AbstractCtor() { } } // probe: S3442

public class PrivateCtorOnly { private PrivateCtorOnly() { } } // probe: S3453

public sealed class EqualsOnly { public override bool Equals(object? o) => true; } // probe: S1206

public sealed class RefEquals { public static bool operator ==(RefEquals? a, RefEquals? b) => true; public static bool operator !=(RefEquals? a, RefEquals? b) => false; public override bool Equals(object? o) => true; public override int GetHashCode() => 1; } // probe: S3875

public sealed class ToStringNull { public override string ToString() { /* probe: S2225 */ return null; } }

public sealed class EqualsThrows { public override bool Equals(object? o) { /* probe: S3877 */ throw new NotSupportedException(); } public override int GetHashCode() => 1; }

public class NotSealedEq : IEquatable<NotSealedEq> { public bool Equals(NotSealedEq? o) => true; public override bool Equals(object? o) => true; public override int GetHashCode() => 1; } // probe: S4035

public sealed class EqualsNoIface { public bool Equals(EqualsNoIface other) => true; } // probe: S3897

public sealed class BaseEquals { public override bool Equals(object? o) { /* probe: S3249 */ return base.Equals(o); } public override int GetHashCode() => 1; }

public sealed class Comparable : IComparable<Comparable> { public int CompareTo(Comparable? other) => 0; } // probe: S1210

public sealed class AttrNoUsage : Attribute { } // probe: S3993

public sealed class NotAnException { } // probe: S2166
public sealed class FakeException { } // probe: S2166

public sealed class OldBase : ApplicationException { } // probe: S4052

internal sealed class PrivateException : Exception { } // probe: S3871

public sealed class LockTypes { public void M(System.Reflection.MemberInfo info) { /* probe: S3998 */ lock (info) { } } }

// probe: S3881
public class Disposer : IDisposable
{
    public void Dispose() { }
}

public sealed class SuppressFin : IDisposable { public void Dispose() { } public void Other() { /* probe: S3971 */ GC.SuppressFinalize(this); } }

public sealed class HandleUse { public IntPtr M(Microsoft.Win32.SafeHandles.SafeFileHandle h) { /* probe: S3869 */ return h.DangerousGetHandle(); } }

public sealed class AssemblyLoad { public void M() { /* probe: S3885 */ System.Reflection.Assembly.LoadFrom("x.dll"); } }

public sealed class ThreadOld { public void M(Thread t) { /* probe: S3889 */ t.Suspend(); } }

public sealed class Yielder { public IEnumerable<int> M(string s) { /* probe: S4456 */ ArgumentNullException.ThrowIfNull(s); yield return 1; } }

// probe: S4545
[DebuggerDisplay("{Missing}")]
public sealed class DbgDisplayBad { }

public sealed class Disposables
{
    public void Leak() { /* probe: S2930 */ FileStream fs = new("x", FileMode.Open); fs.WriteByte(1); }
    public MemoryStream Returned() { /* probe: S2997 */ using MemoryStream ms = new(); return ms; }
}

public sealed class Statics
{
    // probe: S3263
    public static int First = Second + 1;
    public static int Second = 2;
}

public sealed class StaticCtor
{
    // probe: S3963
    public static int Value;
    static StaticCtor() { Value = 1; }
}

public class ProbeVirtual
{
    public ProbeVirtual() { /* probe: S1699 */ Virt(); }
    public virtual void Virt() { }
}

public class BaseWithDefault { public virtual void M(int a = 1) { } public virtual void P(int[] a) { } public virtual void Q(int a = 1) { } }

public sealed class DerivedDefault : BaseWithDefault
{
    // probe: S1006
    public override void M(int a = 2) { }

    // probe: S3600
    public override void P(params int[] a) { }

    // probe: S3466
    public override void Q(int a = 1) { base.Q(); }
}

public class BaseNames { public virtual void M(int alpha) { } public virtual void Run() { } }

public sealed class DerivedNames : BaseNames
{
    // probe: S927
    public override void M(int beta) { }

    // probe: S1185
    public override void Run() { base.Run(); }
}

public sealed class Ctor3220
{
    // probe: S3220
    public void Call() { Over(null); }
    public void Over(object o) { }
    public void Over(params object[] o) { }
}

public class Events
{
    // probe: S2290
    public virtual event EventHandler? Virtual;

    // probe: S3244
    public event EventHandler? Changed;
    public void Unsubscribe() { Changed -= (s, e) => { }; }
}

public sealed class Params { public void M(__arglist) { } } // probe: S4061



public sealed class Tab { public string M() { /* probe: S2479 */ return "a	b"; } }

