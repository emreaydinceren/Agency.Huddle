namespace Agency.Huddle.App.Components;

/// <summary>Which membership chip a <see cref="Shared.ProfileCard"/> draws.</summary>
public enum ProfileMembership
{
    /// <summary>No chip.</summary>
    None,

    /// <summary>The "Member" chip: already on the Team.</summary>
    Member,

    /// <summary>The "New" chip: added in this session and not yet saved.</summary>
    New,
}
