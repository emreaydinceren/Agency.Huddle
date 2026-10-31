namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>The resolved Session settings (Spec 5.1): what a fresh session rereads of a Room.</summary>
/// <param name="CatchUpMessages">The most Messages a fresh session rereads.</param>
/// <param name="CatchUpTokens">The estimated size, in tokens, of what a fresh session rereads.</param>
internal sealed record SessionSettings(int CatchUpMessages, int CatchUpTokens);
