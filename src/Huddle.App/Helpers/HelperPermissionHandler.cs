using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.App.Helpers;

/// <summary>
/// Refuses every permission request a Helper's session makes, and counts them: a Helper has no tools. It picks the
/// one-time refusal, then the permanent one, then cancels, as <c>PlanModePermissionHandler</c> does.
/// </summary>
internal sealed class HelperPermissionHandler : IPermissionHandler
{
    private int refused;

    /// <summary>How many requests were refused.</summary>
    public int Refused => Volatile.Read(ref this.refused);

    /// <inheritdoc/>
    public Task<PermissionDecision> DecideAsync(PermissionRequestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        Interlocked.Increment(ref this.refused);

        PermissionOptionInfo? refusal = context.Options.FirstOrDefault(static option => option.Kind == PermissionOptionKind.RejectOnce)
            ?? context.Options.FirstOrDefault(static option => option.Kind == PermissionOptionKind.RejectAlways);
        PermissionDecision decision = refusal is null ? PermissionDecision.Cancelled : new SelectedDecision(refusal.OptionId);
        return Task.FromResult(decision);
    }
}
