namespace Agency.Huddle.App.Sidebar;

#pragma warning restore S1694

/// <summary>A Room is open.</summary>
/// <param name="RoomId">The Room's id.</param>
public sealed record RoomSubject(string RoomId) : PageSubject
{
    /// <inheritdoc />
    public override string Key => "room:" + this.RoomId;
}
