namespace Agency.Huddle.App.Helpers;

/// <summary>How a judgement ended.</summary>
internal enum HelperRunKind
{
    /// <summary>The Verdict was applied, or deliberately not (no change, dropped).</summary>
    Applied,

    /// <summary>The call failed, the answer was unusable, or the change was refused.</summary>
    Failed,

    /// <summary>The daily cap was reached; nothing ran.</summary>
    OverBudget,
}
