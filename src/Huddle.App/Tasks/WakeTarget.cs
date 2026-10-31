namespace Agency.Huddle.App.Tasks;

/// <summary>Who a Request transition wakes.</summary>
internal enum WakeTarget
{
    /// <summary>The manager of the Team that filed the Request.</summary>
    RequestingManager,

    /// <summary>The manager of the Team that received the Request.</summary>
    ReceivingManager,

    /// <summary>The Chief of Staff built-in Agent.</summary>
    ChiefOfStaff,
}
