namespace Agency.Huddle.App.Feed;

/// <summary>A Memory or outputs folder holds more files than Agents are shown (Spec §6.8).</summary>
/// <param name="Folder">The folder, for example <c>Teams/Home/memory</c>.</param>
/// <param name="Message">The Notification text (Settled text T14).</param>
internal sealed record SecondBrainOverCapPayload(string Folder, string Message) : FeedPayload;
