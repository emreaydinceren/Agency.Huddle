namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>Read access to the resolved Session settings, so consumers depend on the values and not on the file behind them.</summary>
internal interface ISessionSettings
{
    /// <summary>The settings now in force: each value is the user's override when one is set and valid, otherwise the configured default.</summary>
    SessionSettings Current { get; }
}
