namespace Agency.Huddle.App.Acp;

/// <summary>
/// Tracks Teammate-folder moves in flight, so a caller about to create or read a Teammate's folder
/// can wait for a pending rename to finish first, rather than racing
/// <see cref="PersonaRenameCascade"/>'s detached move - see corrections-B2 item 20. A singleton,
/// shared between <see cref="PersonaRenameCascade"/> (which signals around each move) and
/// <c>DotAcpAgentHostFactory</c> (which waits on it before creating a Persona's Work Dir).
/// </summary>
internal sealed class TeammateFolderMoves
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, TaskCompletionSource> pending = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Marks a move to <paramref name="newName"/> as pending. Pass the returned handle to
    /// <see cref="Complete"/> once the move finishes, whether it succeeded or gave up.
    /// </summary>
    /// <param name="newName">The Teammate's new Name, compared case-insensitively by <see cref="WhenSettledAsync"/>.</param>
    /// <returns>A handle identifying this pending move.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="newName"/> is empty or whitespace.</exception>
    public TaskCompletionSource Begin(string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (this.gate)
        {
            this.pending[newName] = tcs;
        }

        return tcs;
    }

    /// <summary>Marks the move <paramref name="handle"/> identifies as finished, releasing anything waiting on it in <see cref="WhenSettledAsync"/>.</summary>
    /// <param name="handle">The handle <see cref="Begin"/> returned.</param>
    public void Complete(TaskCompletionSource handle)
    {
        ArgumentNullException.ThrowIfNull(handle);

        lock (this.gate)
        {
            foreach (var (name, value) in this.pending)
            {
                if (ReferenceEquals(value, handle))
                {
                    _ = this.pending.Remove(name);
                    break;
                }
            }
        }

        _ = handle.TrySetResult();
    }

    /// <summary>
    /// Waits for any move to <paramref name="name"/> that is currently pending to finish. Completes
    /// synchronously when nothing is pending for that Name, rather than handing back a Task that
    /// only resolves later.
    /// </summary>
    /// <param name="name">The Teammate Name to check, compared case-insensitively.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A Task that completes once no move for <paramref name="name"/> is pending.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="name"/> is empty or whitespace.</exception>
    public Task WhenSettledAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        TaskCompletionSource? tcs;
        lock (this.gate)
        {
            _ = this.pending.TryGetValue(name, out tcs);
        }

        return tcs is null ? Task.CompletedTask : tcs.Task.WaitAsync(cancellationToken);
    }
}
