namespace Agency.Huddle.App.Teams;

/// <summary>Reports whether a connector, such as <c>email:read</c>, is connected.</summary>
internal interface IConnectorStatus
{
    /// <summary>Tells whether <paramref name="connector"/> is connected.</summary>
    /// <param name="connector">The connector id from a role's <c>requires</c>.</param>
    /// <returns><see langword="true"/> when the connector is connected.</returns>
    bool IsConnected(string connector);

    /// <summary>Where a user records a connected connector, or <see langword="null"/> when there is no such place.</summary>
    string? Location => null;
}
