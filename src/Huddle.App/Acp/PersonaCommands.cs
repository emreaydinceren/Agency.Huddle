namespace Agency.Huddle.App.Acp;

/// <summary>One Adapter command a Teammate currently offers: advertised by its Adapter and allowed by its Adapter Profile.</summary>
/// <param name="Name">The command's name without the leading slash, in the Adapter's own casing, which is what is sent back to it.</param>
/// <param name="Description">The Adapter's own one-line description. Text only: never rendered as Markdown or HTML.</param>
/// <param name="InputHint">The Adapter's placeholder for the command's free-text input, or <see langword="null"/> when it takes none.</param>
internal sealed record AdapterCommand(string Name, string Description, string? InputHint);

/// <summary>
/// The Adapter commands each Persona currently offers, in memory, replaced whole each time its Adapter
/// advertises a list (Commands spec, section 6.3). It is filtered at ingest, so the full advertised list,
/// which includes the Human's own skills when isolation is off, is never held here or shown. It persists
/// nothing: a restart starts empty and the Adapter's first update refills it.
/// </summary>
/// <remarks>
/// Keyed by Persona, not by Room Session: the list comes from the Adapter's environment and the Work
/// Dir, both per Persona, so several Room Sessions of one Persona advertise the same list.
/// </remarks>
/// <param name="logger">Records a <see cref="CommandsChanged"/> subscriber that threw.</param>
internal sealed class PersonaCommands(ILogger<PersonaCommands> logger)
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, AdapterCommand[]> personas = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Raised, outside the lock, after a Persona's offered commands actually changed, with the Persona's
    /// Name as it was reported. Never raised for an identical list, so a repeated advertisement does not
    /// repaint a card.
    /// </summary>
    internal event Action<string>? CommandsChanged;

    /// <summary>How many handlers are subscribed to <see cref="CommandsChanged"/>. Test seam only: it proves a disposed card unsubscribed.</summary>
    internal int SubscriberCount => this.CommandsChanged?.GetInvocationList().Length ?? 0;

    /// <summary>Replaces the whole list <paramref name="personaName"/> offers.</summary>
    /// <param name="personaName">The Persona, compared without regard to case.</param>
    /// <param name="offered">Every command the Persona now offers; empty clears it.</param>
    internal void Set(string personaName, IReadOnlyList<AdapterCommand> offered)
    {
        AdapterCommand[] next = [.. offered];
        bool changed;
        lock (this.gate)
        {
            AdapterCommand[] current = this.personas.GetValueOrDefault(personaName) ?? [];
            changed = !current.AsSpan().SequenceEqual(next);
            if (changed)
            {
                if (next.Length == 0)
                {
                    this.personas.Remove(personaName);
                }
                else
                {
                    this.personas[personaName] = next;
                }
            }
        }

        if (changed)
        {
            this.RaiseCommandsChanged(personaName);
        }
    }

    /// <summary>The command <paramref name="personaName"/> offers under <paramref name="name"/>.</summary>
    /// <param name="personaName">The Persona, compared without regard to case.</param>
    /// <param name="name">The command's name without the slash, compared without regard to case.</param>
    /// <returns>The command with the Adapter's own casing, or <see langword="null"/> when it is not offered.</returns>
    internal AdapterCommand? Find(string personaName, string name)
    {
        lock (this.gate)
        {
            if (!this.personas.TryGetValue(personaName, out AdapterCommand[]? offered))
            {
                return null;
            }

            return Array.Find(offered, command => string.Equals(command.Name, name, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>The commands <paramref name="personaName"/> offers.</summary>
    /// <param name="personaName">The Persona, compared without regard to case.</param>
    /// <returns>A snapshot; empty when the Persona offers none, so a card shows no line rather than a heading.</returns>
    internal IReadOnlyList<AdapterCommand> Get(string personaName)
    {
        lock (this.gate)
        {
            return this.personas.TryGetValue(personaName, out AdapterCommand[]? offered) ? Array.AsReadOnly(offered) : [];
        }
    }

    /// <summary>Moves a renamed Persona's list to its new Name.</summary>
    /// <param name="oldName">The Name the Persona had.</param>
    /// <param name="newName">The Name it has now.</param>
    internal void Rename(string oldName, string newName)
    {
        lock (this.gate)
        {
            if (this.personas.Remove(oldName, out AdapterCommand[]? offered))
            {
                this.personas[newName] = offered;
            }
        }
    }

    /// <summary>Forgets a removed or stopped Persona's list, so its card shows none and a stopped Teammate offers nothing.</summary>
    /// <param name="personaName">The Persona, compared without regard to case.</param>
    internal void Forget(string personaName)
    {
        bool removed;
        lock (this.gate)
        {
            removed = this.personas.Remove(personaName);
        }

        if (removed)
        {
            this.RaiseCommandsChanged(personaName);
        }
    }

    private void RaiseCommandsChanged(string personaName)
    {
        Action<string>? handlers = this.CommandsChanged;
        if (handlers is null)
        {
            return;
        }

        foreach (Action<string> handler in handlers.GetInvocationList().Cast<Action<string>>())
        {
            try
            {
                handler(personaName);
            }
            catch (Exception ex)
            {
                // One broken subscriber, typically a card that was disposed mid-event, must not stop the others.
                logger.LogWarning(ex, "A commands-changed subscriber threw for persona '{PersonaName}'.", personaName);
            }
        }
    }
}
