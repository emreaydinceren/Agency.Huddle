namespace Agency.Huddle.App.Services;

/// <summary>What the rest of the app asks of the Turn Queue as a lifecycle event (Turn Queue spec §5.6).</summary>
internal interface ITurnQueueControl
{
    /// <summary>Closes the running rows of a connection that ended as stopped, unless the host itself is stopping.</summary>
    /// <param name="connectionId">The connection that ended.</param>
    void CloseRunning(string connectionId);
}
