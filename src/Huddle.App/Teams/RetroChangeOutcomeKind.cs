namespace Agency.Huddle.App.Teams;

/// <summary>What accepting or rejecting one retro change did.</summary>
internal enum RetroChangeOutcomeKind
{
    /// <summary>The edit was made and the register row recorded.</summary>
    Applied,

    /// <summary>Nothing was changed because the text did not match exactly once, or the file could not be written; the change stays pending.</summary>
    Refused,

    /// <summary>The Human rejected the change; nothing was written.</summary>
    Rejected,

    /// <summary>The change was already taken by another click, or never existed in this room.</summary>
    Gone,
}
