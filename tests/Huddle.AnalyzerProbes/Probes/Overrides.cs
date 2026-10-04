namespace Agency.Huddle.AnalyzerProbes;

public class OverrideParamsBase { public virtual void Z(params int[] a) { } }

public class OverrideParamsDerived : OverrideParamsBase
{
    // probe: S3262
    public override void Z(int[] a) { }
}
