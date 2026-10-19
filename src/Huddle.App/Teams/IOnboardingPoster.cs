namespace Agency.Huddle.App.Teams;

/// <summary>Delivers a message as the Human to a manager's direct Room, so it becomes the manager's next Turn.</summary>
internal interface IOnboardingPoster
{
    /// <summary>Posts <paramref name="text"/> to the manager's Room.</summary>
    /// <param name="managerName">The manager Teammate's Name.</param>
    /// <param name="text">The message.</param>
    /// <param name="ct">Cancels the post.</param>
    /// <returns><see langword="true"/> when the message was posted; <see langword="false"/> when the manager or its Room could not be reached.</returns>
    Task<bool> PostAsync(string managerName, string text, CancellationToken ct);

    /// <summary>
    /// Posts <paramref name="text"/> for <paramref name="recipientName"/> in <paramref name="team"/>'s Team Room, prefixed by a
    /// Mention of the recipient, when that Room exists, is not Archived and has the recipient as a Member; otherwise in the
    /// recipient's root one-to-one, exactly as <see cref="PostAsync"/> does.
    /// </summary>
    /// <param name="team">The Team the post is for.</param>
    /// <param name="recipientName">The recipient Teammate's Name.</param>
    /// <param name="text">The message.</param>
    /// <param name="ct">Cancels the post.</param>
    /// <returns><see langword="true"/> when the message was posted; <see langword="false"/> when the recipient or a Room could not be reached.</returns>
    Task<bool> PostToTeamAsync(string team, string recipientName, string text, CancellationToken ct);
}
