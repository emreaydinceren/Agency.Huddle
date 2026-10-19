namespace Agency.Huddle.App.Teams;

/// <summary>The status of a retro register row (RT-6, Spec §Retro).</summary>
internal enum RetroStatus
{
    /// <summary>Found, nothing changed yet.</summary>
    Open,

    /// <summary>The change was made; not yet checked against later evidence.</summary>
    Done,

    /// <summary>The change only partly worked.</summary>
    Partial,

    /// <summary>Later evidence shows the change worked.</summary>
    Verified,

    /// <summary>The change was replaced by another one.</summary>
    Changed,
}
