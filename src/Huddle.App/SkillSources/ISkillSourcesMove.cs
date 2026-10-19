namespace Agency.Huddle.App.SkillSources;

/// <summary>The one-time move from the old format (§6.13). <see cref="RegistryWatcher"/> runs it before its first read.</summary>
internal interface ISkillSourcesMove
{
    /// <summary>Runs the move when SKILLS.md does not exist; otherwise returns at once.</summary>
    Task RunAsync(CancellationToken ct);
}
