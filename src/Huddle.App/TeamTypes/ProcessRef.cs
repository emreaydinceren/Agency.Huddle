namespace Agency.Huddle.App.TeamTypes;

/// <summary>A process document a team type offers.</summary>
/// <param name="Id">The process id.</param>
/// <param name="File">The file under the type's processes folder.</param>
/// <param name="Default">Whether this process is used when none is chosen.</param>
internal sealed record ProcessRef(string Id, string File, bool Default = false);
