namespace Agency.Huddle.Contracts;

/// <summary>The wire protocol version this build speaks, per <see cref="ProtocolJson"/>.</summary>
public static class ProtocolVersion
{
    /// <summary>The current protocol version. A strict-equality check against every parsed envelope.</summary>
    public const int Current = 3;
}