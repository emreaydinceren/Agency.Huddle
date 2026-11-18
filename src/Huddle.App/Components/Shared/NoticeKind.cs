namespace Agency.Huddle.App.Components;

/// <summary>
/// Whether a <c>Notice</c> interrupts or reports, which fixes the role it is announced with (ComponentCatalog, Notice; Q-A1-1).
/// Public because it is a component parameter's type.
/// </summary>
public enum NoticeKind
{
    /// <summary>The Human must act now (a conflict, a refusal, a failure, a missing Team): <c>role="alert"</c>, read out at once.</summary>
    Interruption,

    /// <summary>The result or state of something, which the Human may act on later: <c>role="status"</c>, read out politely.</summary>
    Consequence,
}
