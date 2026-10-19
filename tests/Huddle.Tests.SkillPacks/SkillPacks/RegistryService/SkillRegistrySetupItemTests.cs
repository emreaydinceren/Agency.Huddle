using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.SkillPacks.RegistryService;

/// <summary>Pins <see cref="Agency.Huddle.App.SkillSources.SkillRegistry.SetupItem"/> against real files: the detail each
/// not-ready state carries, and null for what is ready or no row provides.</summary>
public sealed class SkillRegistrySetupItemTests
{
    /// <summary>A Pack whose Skill has no path here is NotDownloaded with the Pack's title; its Skill is NotDownloaded too.</summary>
    [Fact]
    public async Task SetupItem_PackNotDownloaded_CarriesTheTitle()
    {
        using RegistryHost host = new();
        host.Source();
        host.Pack("documents", "docx", null);
        host.SkillsMd(["packs/documents.skillpack.json"]);
        await ReadAsync(host);

        Assert.Equal(new SkillSetupItem(SkillSetupKind.Pack, "documents", SkillAvailability.NotDownloaded, "documents title"), host.Registry.SetupItem(SkillSetupKind.Pack, "documents"));
        Assert.Equal(SkillAvailability.NotDownloaded, host.Registry.SetupItem(SkillSetupKind.Skill, "docx")?.Problem);
    }

    /// <summary>A withdrawn Source's Pack and Skill carry the Source's reason.</summary>
    [Fact]
    public async Task SetupItem_Withdrawn_CarriesTheSourceReason()
    {
        using RegistryHost host = new();
        host.Source("withdrawn");
        host.Pack("documents", "docx", host.CloneSkill("docx"));
        host.SkillsMd(["packs/documents.skillpack.json"]);
        await ReadAsync(host);

        Assert.Equal(new SkillSetupItem(SkillSetupKind.Pack, "documents", SkillAvailability.Withdrawn, "Because."), host.Registry.SetupItem(SkillSetupKind.Pack, "documents"));
        Assert.Equal(new SkillSetupItem(SkillSetupKind.Skill, "docx", SkillAvailability.Withdrawn, "Because."), host.Registry.SetupItem(SkillSetupKind.Skill, "docx"));
    }

    /// <summary>A usable Pack and Skill, a built-in Skill and a Pack no row provides all give null; an unknown Skill does not.</summary>
    [Fact]
    public async Task SetupItem_ReadyOrNotProvided_IsNull()
    {
        using RegistryHost host = new();
        host.Source();
        host.Pack("documents", "docx", host.CloneSkill("docx"));
        host.SkillsMd(["packs/documents.skillpack.json"]);
        await ReadAsync(host);

        Assert.Null(host.Registry.SetupItem(SkillSetupKind.Pack, "documents"));
        Assert.Null(host.Registry.SetupItem(SkillSetupKind.Skill, "docx"));
        Assert.Null(host.Registry.SetupItem(SkillSetupKind.Skill, "team-building"));
        Assert.Null(host.Registry.SetupItem(SkillSetupKind.Pack, "no-such-pack"));
        Assert.Equal(SkillAvailability.Unknown, host.Registry.SetupItem(SkillSetupKind.Skill, "no-such-skill")?.Problem);
    }

    /// <summary>Reads the registry with the clock far enough ahead that every file is quiet.</summary>
    private static Task ReadAsync(RegistryHost host)
    {
        host.Clock.SetUtcNow(DateTimeOffset.UtcNow.AddHours(1));
        return host.Registry.ReloadAsync(TestContext.Current.CancellationToken);
    }
}
