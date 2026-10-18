using Agency.Huddle.App.SkillSources;

namespace Agency.Huddle.Tests.SkillPacks.Prompts;

/// <summary>A fixed install file location.</summary>
/// <param name="InstallFilePath">The path.</param>
internal sealed record FixedInstallFile(string InstallFilePath) : IInstallFileLocation;
