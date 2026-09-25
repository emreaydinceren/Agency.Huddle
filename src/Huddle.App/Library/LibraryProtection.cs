namespace Agency.Huddle.App.Library;

/// <summary>Restrictions on renaming, moving, or deleting an item in the Library.</summary>
/// <param name="CanRename">Whether the item can be renamed.</param>
/// <param name="CanMove">Whether the item can be moved.</param>
/// <param name="CanDelete">Whether the item can be deleted.</param>
/// <param name="Reason">If any action is forbidden, a user-facing reason (e.g., "Team folders are managed from Tasks").</param>
internal sealed record LibraryProtection(bool CanRename, bool CanMove, bool CanDelete, string? Reason);
