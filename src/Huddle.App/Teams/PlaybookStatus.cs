namespace Agency.Huddle.App.Teams;

/// <summary>Where one Playbook stands for one Team (Team Playbooks spec §3.3).</summary>
internal enum PlaybookStatus
{
    /// <summary>No file: the Team reads the effective default.</summary>
    Default,

    /// <summary>A file whose <c>basedOn</c> is the hash of the effective default.</summary>
    Customized,

    /// <summary>A file whose <c>basedOn</c> differs from the hash of the effective default; its body still applies.</summary>
    DefaultUpdated,

    /// <summary>A file with no readable <c>basedOn</c>, made by hand.</summary>
    UnknownBase,

    /// <summary>A file that exists and cannot be read; the Team reads the effective default.</summary>
    Unreadable,
}
