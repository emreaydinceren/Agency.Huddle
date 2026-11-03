namespace Agency.Huddle.App.Library;

/// <summary>Bound from <c>Team:Library:Archive</c>, per Spec §6.9 and §7.4: the Archive root folder, where put-away Teams and Projects are kept.</summary>
public sealed class LibraryArchiveOptions
{
    /// <summary>The Archive root's folder, relative to <c>DataDir</c> (<c>Team:Library:Archive:Dir</c>).</summary>
    public string Dir { get; set; } = "Archive";
}
