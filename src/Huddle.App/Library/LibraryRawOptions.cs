namespace Agency.Huddle.App.Library;

/// <summary>Bound from <c>Team:Library:Raw</c>, per Spec §6.1 and §7.4: the Raw root folder, where files brought into the Team are kept as they arrived.</summary>
public sealed class LibraryRawOptions
{
    /// <summary>The Raw root's folder, relative to <c>DataDir</c> (<c>Team:Library:Raw:Dir</c>).</summary>
    public string Dir { get; set; } = "Raw";
}
