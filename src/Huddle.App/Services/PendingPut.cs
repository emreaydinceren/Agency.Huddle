namespace Agency.Huddle.App.Services;

/// <summary>
/// What <see cref="PendingByRoom{T}.TryPut"/> did, and - only for <see cref="PendingPutResult.Refused"/> - the
/// item already pending, so the caller can name its owner.
/// </summary>
/// <typeparam name="T">The pending item.</typeparam>
/// <param name="Result">Which of the three outcomes occurred.</param>
/// <param name="Existing">The item already pending when <paramref name="Result"/> is <see cref="PendingPutResult.Refused"/>; otherwise <see langword="null"/>.</param>
internal sealed record PendingPut<T>(PendingPutResult Result, T? Existing)
    where T : class;
