namespace Agency.Huddle.App.Teams;

/// <summary>Determines which tabs appear on Team and Project pages, and resolves URL segments to tabs.</summary>
internal static class TeamPageTabs
{
    /// <summary>Returns the tabs available for a Team or Project based on enabled features.</summary>
    /// <param name="isProject">True for Project tabs, false for Team tabs.</param>
    /// <param name="features">The enabled features (Library and Tasks).</param>
    /// <returns>The list of available tabs in order.</returns>
    public static IReadOnlyList<TeamPageTab> Available(bool isProject, TeamPageFeatures features)
    {
        ArgumentNullException.ThrowIfNull(features);

        if (isProject)
        {
            if (features.Library && features.Tasks)
            {
                return [TeamPageTab.Files, TeamPageTab.Tasks];
            }
            else if (features.Library)
            {
                return [TeamPageTab.Files];
            }
            else if (features.Tasks)
            {
                return [TeamPageTab.Tasks];
            }
            else
            {
                return [];
            }
        }
        else
        {
            // Team pages always have Members
            List<TeamPageTab> tabs = [TeamPageTab.Members];
            if (features.Library)
            {
                tabs.Add(TeamPageTab.Files);
            }

            // A Team page has no Tasks tab: the sidebar's TASKS section lists the Team's Task Views instead.
            if (features.Process)
            {
                tabs.Add(TeamPageTab.Process);
            }

            // Playbooks need neither the Library nor Tasks to be read, so both tabs are always on a Team page (Team Playbooks spec §7.1).
            // Each Playbook has its own tab: the Manager's first, then the Agents'.
            tabs.Add(TeamPageTab.ManagerPlaybook);
            tabs.Add(TeamPageTab.AgentPlaybook);

            // Team pages always end with Details: the type, what it's for and the Library folder
            tabs.Add(TeamPageTab.Details);
            return tabs;
        }
    }

    /// <summary>Resolves a URL segment to a tab, falling back to the first available tab if not found.</summary>
    /// <param name="isProject">True for Project tab resolution, false for Team tab resolution.</param>
    /// <param name="tab">The URL segment to resolve (case-insensitive), or null/empty.</param>
    /// <param name="features">The enabled features (Library and Tasks).</param>
    /// <returns>The resolved tab, or the first available tab if not found.</returns>
    public static TeamPageTab Resolve(bool isProject, string? tab, TeamPageFeatures features)
    {
        ArgumentNullException.ThrowIfNull(features);

        IReadOnlyList<TeamPageTab> available = Available(isProject, features);

        // Empty or null tab: return first available or default
        if (string.IsNullOrEmpty(tab))
        {
            if (available.Count > 0)
            {
                return available[0];
            }

            // No available tabs; return Files for projects, should not happen for teams
            return TeamPageTab.Files;
        }

        // Try to match against the segment of each available tab
        foreach (TeamPageTab tabOption in available)
        {
            if (Segment(tabOption).Equals(tab, StringComparison.OrdinalIgnoreCase))
            {
                return tabOption;
            }
        }

        // No match found: return first available
        if (available.Count > 0)
        {
            return available[0];
        }

        // No available tabs; return Files as default
        return TeamPageTab.Files;
    }

    /// <summary>Returns the label a tab shows: the Files tab reads "Library", as the sidebar's section does; the others their own name.</summary>
    /// <param name="tab">The tab to label.</param>
    /// <returns>"Members", "Library", "Tasks", "Process", "Manager Playbook", "Agent Playbook" or "Details".</returns>
    public static string Label(TeamPageTab tab) => tab switch
    {
        TeamPageTab.Members => "Members",
        TeamPageTab.Files => "Library",
        TeamPageTab.Tasks => "Tasks",
        TeamPageTab.Process => "Process",
        TeamPageTab.ManagerPlaybook => "Manager Playbook",
        TeamPageTab.AgentPlaybook => "Agent Playbook",
        _ => "Details",
    };

    /// <summary>Returns the URL segment for a tab.</summary>
    /// <param name="tab">The tab to convert to a segment.</param>
    /// <returns>"members", "files", "tasks", "process", "manager-playbook", "agent-playbook" or "details".</returns>
    public static string Segment(TeamPageTab tab) => tab switch
    {
        TeamPageTab.Members => "members",
        TeamPageTab.Files => "files",
        TeamPageTab.Tasks => "tasks",
        TeamPageTab.Process => "process",
        TeamPageTab.ManagerPlaybook => "manager-playbook",
        TeamPageTab.AgentPlaybook => "agent-playbook",
        TeamPageTab.Details => "details",
        _ => "members",
    };
}
