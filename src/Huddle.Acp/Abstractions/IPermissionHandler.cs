namespace Agency.Huddle.Acp.Abstractions;

using System.Threading;
using System.Threading.Tasks;

/// <summary>Decides how to respond to a permission request raised by the agent.</summary>
public interface IPermissionHandler
{
    Task<PermissionDecision> DecideAsync(PermissionRequestContext context, CancellationToken cancellationToken);
}