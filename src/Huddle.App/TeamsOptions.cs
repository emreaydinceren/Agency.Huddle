namespace Agency.Huddle.App;

/// <summary>Bound from <c>Team:Teams</c>, per Spec §7. Shared with the Tasks feature (Tasks.Dir will be removed in D3).</summary>
public sealed class TeamsOptions
{
    /// <summary>Team folders, relative to <c>DataDir</c>. Nothing reads this configuration key until D3/D4; it will replace <c>Tasks:Dir</c>.</summary>
    public string Dir { get; set; } = "Teams";
}
