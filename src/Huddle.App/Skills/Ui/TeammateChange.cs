namespace Agency.Huddle.App.Skills.Ui;

/// <summary>What one holder's file would change.</summary>
/// <param name="Name">The Teammate.</param>
/// <param name="Role">Its title.</param>
/// <param name="AddSkills">Skills added to its <c>skills:</c>.</param>
/// <param name="RemoveSkills">Skills taken off.</param>
/// <param name="ConsultWhenChanges">Whether its <c>consult_when</c> gains the Pack's phrase.</param>
/// <param name="VersionChanges">Whether its <c>_packs</c> version changes.</param>
internal sealed record TeammateChange(string Name, string Role, IReadOnlyList<string> AddSkills, IReadOnlyList<string> RemoveSkills, bool ConsultWhenChanges, bool VersionChanges)
{
    /// <summary>What the row says changes: "adds a and b; restarts".</summary>
    public string Summary
    {
        get
        {
            List<string> parts = [];
            if (this.AddSkills.Count > 0)
            {
                parts.Add($"adds {SkillStateText.AndList(this.AddSkills)}");
            }

            if (this.RemoveSkills.Count > 0)
            {
                parts.Add($"removes {SkillStateText.AndList(this.RemoveSkills)}");
            }

            if (this.ConsultWhenChanges)
            {
                parts.Add("updates consult when");
            }

            if (parts.Count == 0 && this.VersionChanges)
            {
                parts.Add("records the new version");
            }

            parts.Add("restarts");
            return string.Join("; ", parts);
        }
    }
}
