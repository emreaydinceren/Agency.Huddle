namespace Agency.Huddle.App.Desktop;

/// <summary>
/// The record a running desktop instance publishes so that a second launch and <c>huddle stop</c> can find it.
/// </summary>
/// <param name="Url">The loopback URL the instance listens on.</param>
/// <param name="StopToken">The token that authenticates <c>POST /_desktop/stop</c>.</param>
/// <param name="Pid">The operating-system process id of the instance.</param>
internal sealed record InstanceInfo(string Url, string StopToken, int Pid);
