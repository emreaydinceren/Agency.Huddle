namespace Agency.Huddle.App.Services;

/// <summary>The outcome of <see cref="PendingByRoom{T}.TryPut"/>.</summary>
internal enum PendingPutResult
{
    /// <summary>Nothing was pending in the Room; the new item is stored.</summary>
    Stored,

    /// <summary>The same owner's earlier item in this Room was replaced.</summary>
    Replaced,

    /// <summary>Another owner's item is already pending in this Room; the new one was refused.</summary>
    Refused,
}
