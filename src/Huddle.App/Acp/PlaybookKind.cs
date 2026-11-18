namespace Agency.Huddle.App.Acp;

/// <summary>Which Playbook a <see cref="PlaybookSection"/> belongs to (Team Playbooks spec §3).</summary>
internal enum PlaybookKind
{
    /// <summary>The Playbook every member of a Team reads.</summary>
    Member,

    /// <summary>The Playbook only the manager of a Team reads, after the Member one.</summary>
    Manager,
}
