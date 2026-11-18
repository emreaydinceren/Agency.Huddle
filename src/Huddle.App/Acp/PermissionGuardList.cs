namespace Agency.Huddle.App.Acp;

/// <summary>Holds the one place that fixes the order in which one Agent session's permission handler consults its guards.</summary>
internal static class PermissionGuardList
{
    /// <summary>
    /// Puts the guards in the order they are consulted: the <c>~/.claude</c> directory, <c>AGENTS.md</c>, Raw, Prompts, Team Memory, the
    /// Agent's settings, then the merge gate (Spec §6.6). A guard that is <see langword="null"/> is absent from the list.
    /// </summary>
    /// <param name="agentsMd">Refuses a write to the Human's <c>AGENTS.md</c> files.</param>
    /// <param name="teamMemory">Refuses a write into a Team's Memory by anyone but its manager.</param>
    /// <param name="raw">Refuses a change to an original in a <c>raw/</c> position.</param>
    /// <param name="prompts">Refuses a write to the Prompts folder, which only the Human changes.</param>
    /// <param name="settings">Refuses a write to the Agent's local settings file and the merge gate hook (issue #432).</param>
    /// <param name="humanTeamFiles">Refuses a write to a Team's Playbook files, consulted right after <c>AGENTS.md</c> (Team Playbooks spec §7.6); <see langword="null"/> leaves them unguarded.</param>
    /// <param name="protectedDirectory">Refuses a write into the Agent's own configuration directory; <see langword="null"/> when it cannot be resolved.</param>
    /// <param name="mergeGate">Asks the Team's gate about <c>git merge</c> and <c>git push</c>; <see langword="null"/> leaves them approved.</param>
    /// <param name="renamedFolder">Refuses a write under a renamed Team's old folder, consulted after Team Memory; <see langword="null"/> when no rename journal is registered.</param>
    /// <returns>The guards, in the order the handler consults them.</returns>
    public static IReadOnlyList<IToolCallGuard> InOrder(
        AgentsMdGuard agentsMd,
        TeamMemoryWriteGuard teamMemory,
        RawGuard raw,
        PromptsRootGuard prompts,
        AgentSettingsGuard? settings = null,
        ProtectedDirectoryGuard? protectedDirectory = null,
        MergeGateGuard? mergeGate = null,
        RenamedFolderGuard? renamedFolder = null,
        HumanTeamFilesGuard? humanTeamFiles = null)
    {
        ArgumentNullException.ThrowIfNull(agentsMd);
        ArgumentNullException.ThrowIfNull(teamMemory);
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(prompts);

        List<IToolCallGuard> guards = [];
        AddIfPresent(guards, protectedDirectory);
        guards.Add(agentsMd);
        AddIfPresent(guards, humanTeamFiles);
        guards.Add(raw);
        guards.Add(prompts);
        guards.Add(teamMemory);
        AddIfPresent(guards, renamedFolder);
        AddIfPresent(guards, settings);
        AddIfPresent(guards, mergeGate);
        return guards;
    }

    /// <summary>Adds the guard unless it is absent.</summary>
    /// <param name="guards">The list being built.</param>
    /// <param name="guard">The guard, or <see langword="null"/>.</param>
    private static void AddIfPresent(List<IToolCallGuard> guards, IToolCallGuard? guard)
    {
        if (guard is not null)
        {
            guards.Add(guard);
        }
    }
}
