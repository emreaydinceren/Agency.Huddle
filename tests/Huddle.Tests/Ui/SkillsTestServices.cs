using Agency.Huddle.App.Skills;
using Agency.Huddle.App.SkillSources;
using Agency.Huddle.Tests.SkillPacks.RegistryService;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Registers what Settings › Skills and the Settings page's section badge inject (Skill Packs spec §6.9), so a bUnit
/// context that renders them resolves either the live factory's instances or a <see cref="RegistryHost"/>'s.</summary>
internal static class SkillsTestServices
{
    /// <summary>The registry, its settings, the hidden store, the prompt builder and the path choice, from a live factory.</summary>
    public static void AddFromFactory(IServiceCollection services, TeamWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(factory);
        services.AddSingleton(factory.Services.GetRequiredService<SkillRegistry>());
        services.AddSingleton(factory.Services.GetRequiredService<SkillSourcesSettings>());
        services.AddSingleton(factory.Services.GetRequiredService<HiddenStore>());
        services.AddSingleton(factory.Services.GetRequiredService<RoleLister>());
        services.AddSingleton(factory.Services.GetRequiredService<SkillSourcesRemover>());
        services.AddSingleton(factory.Services.GetRequiredService<PackUpdater>());
        services.AddSingleton(factory.Services.GetRequiredService<SkillPromptBuilder>());
        services.AddScoped<PathFormatChoice>();
    }

    /// <summary>The same services over a <see cref="RegistryHost"/>'s temporary folders, plus its Skill store.</summary>
    public static void AddFromHost(IServiceCollection services, RegistryHost host, IInstallFileLocation? installFile = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(host);
        services.AddSingleton(host.Registry);
        services.AddSingleton(host.Settings);
        services.AddSingleton(host.Hidden);
        services.AddSingleton(host.Roles);
        services.AddSingleton(host.Remover);
        services.AddSingleton(host.Personas);
        services.AddSingleton(new PackUpdater(host.Registry, new SkillCopier(host.Registry, host.Settings, host.Log, Microsoft.Extensions.Logging.Abstractions.NullLogger<SkillCopier>.Instance, host.Clock), host.Personas, host.Log));
        services.AddSingleton<SkillStore>(host.Skills);
        services.AddSingleton(new SkillPromptBuilder(new Agency.Huddle.Tests.SkillPacks.Prompts.RecordingPromptSource(), installFile ?? new FixedInstallFile(Path.Combine(host.Settings.DataDir, "InstallSkillPack.md"))));
        services.AddScoped<PathFormatChoice>();
    }

    /// <summary>An install file at a fixed path.</summary>
    /// <param name="path">The full path of <c>InstallSkillPack.md</c>.</param>
    private sealed class FixedInstallFile(string path) : IInstallFileLocation
    {
        /// <inheritdoc/>
        public string InstallFilePath => path;
    }
}
