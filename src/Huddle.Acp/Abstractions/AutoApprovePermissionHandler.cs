namespace Agency.Huddle.Acp.Abstractions;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Automatically approves permission requests, preferring a one-time grant over a standing grant, and cancelling when neither is offered.</summary>
public sealed class AutoApprovePermissionHandler : IPermissionHandler
{
    public Task<PermissionDecision> DecideAsync(PermissionRequestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (PermissionOptionInfo option in context.Options)
        {
            if (option.Kind == PermissionOptionKind.AllowOnce)
            {
                return Task.FromResult<PermissionDecision>(new SelectedDecision(option.OptionId));
            }
        }

        foreach (PermissionOptionInfo option in context.Options)
        {
            if (option.Kind == PermissionOptionKind.AllowAlways)
            {
                return Task.FromResult<PermissionDecision>(new SelectedDecision(option.OptionId));
            }
        }

        return Task.FromResult(PermissionDecision.Cancelled);
    }
}