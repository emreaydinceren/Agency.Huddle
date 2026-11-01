namespace Agency.Huddle.App.References;

/// <summary>The caps and lengths that bound references (spec §6, §9.2, D-19).</summary>
internal static class ReferenceLimits
{
    /// <summary>The most Task, Room and Team references one Message carries (D-19, OP-6).</summary>
    public const int MaxPerMessage = 10;

    /// <summary>The most candidates of one family the scanner returns before lookup: a hard ceiling, not the cap (finding 2).</summary>
    public const int MaxCandidates = 50;

    /// <summary>The longest label, in text elements, before it is cut (spec §6 label rule 4).</summary>
    public const int MaxLabelChars = 60;

    /// <summary>The longest referenced value quoted in an Agent's header (spec §9.2 step 3).</summary>
    public const int MaxValueChars = 120;

    /// <summary>The most Agent names listed for a Team in an Agent's header (spec §9.1).</summary>
    public const int MaxTeamMemberNames = 8;

    /// <summary>The most Library files one Message carries; the default of <c>Library:MaxReferencedDocuments</c> (D-19).</summary>
    public const int MaxLibraryFiles = 10;
}
