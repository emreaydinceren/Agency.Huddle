namespace Agency.Huddle.Acp.Tests.Fakes;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agency.Huddle.Acp.Abstractions;

/// <summary>A scripted <see cref="IPermissionHandler"/> that records every request it is asked to decide.</summary>
internal sealed class RecordingPermissionHandler : IPermissionHandler
{
    private readonly Lock gate = new Lock();

    private readonly List<PermissionRequestContext> requests = new List<PermissionRequestContext>();

    /// <summary>The decision to return. When <see langword="null"/>, the first offered option is selected.</summary>
    internal PermissionDecision? Decision { get; set; }

    internal IReadOnlyList<PermissionRequestContext> Requests
    {
        get
        {
            lock (this.gate)
            {
                return this.requests.ToArray();
            }
        }
    }

    /// <summary>When set, <see cref="DecideAsync"/> awaits this before returning, so a test can hold a decision open.</summary>
    internal TaskCompletionSource? Gate { get; set; }

    internal bool ThrowOnDecide { get; set; }

    public async Task<PermissionDecision> DecideAsync(PermissionRequestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        lock (this.gate)
        {
            this.requests.Add(context);
        }

        if (this.ThrowOnDecide)
        {
            throw new InvalidOperationException("RecordingPermissionHandler was configured to throw.");
        }

        if (this.Gate is not null)
        {
            await this.Gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        if (this.Decision is not null)
        {
            return this.Decision;
        }

        return new SelectedDecision(context.Options[0].OptionId);
    }
}
