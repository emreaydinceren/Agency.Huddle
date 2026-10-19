namespace Agency.Huddle.App.SkillSources;

/// <summary>Where <c>InstallSkillPack.md</c> is; lets <see cref="SkillPromptBuilder"/> avoid the sealed writer.</summary>
internal interface IInstallFileLocation
{
    /// <summary>The full path of <c>{DataDir}/InstallSkillPack.md</c>.</summary>
    string InstallFilePath { get; }
}
