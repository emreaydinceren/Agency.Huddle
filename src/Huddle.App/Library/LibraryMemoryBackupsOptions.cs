namespace Agency.Huddle.App.Library;

/// <summary>Bound from <c>Team:Library:MemoryBackups</c>: the Memory backups root folder, where each Memory Tidy pass keeps what it changed.</summary>
public sealed class LibraryMemoryBackupsOptions
{
    /// <summary>The Memory backups root's folder, relative to <c>DataDir</c> (<c>Team:Library:MemoryBackups:Dir</c>).</summary>
    public string Dir { get; set; } = "MemoryBackups";
}
