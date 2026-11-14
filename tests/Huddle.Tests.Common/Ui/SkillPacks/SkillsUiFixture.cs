using Agency.Huddle.Tests.SkillPacks.RegistryService;
using Bunit;
using Agency.Huddle.App.Components.Shared;

namespace Agency.Huddle.Tests.Ui.SkillPacks;

/// <summary>A <see cref="RegistryHost"/> behind a bUnit context, for the Settings › Skills components: every service they
/// inject, and the clipboard helper answering with <see cref="ClipboardAccepts"/>.</summary>
internal sealed class SkillsUiFixture : IDisposable
{
    /// <summary>The registry and its files.</summary>
    public RegistryHost Host { get; } = new();

    /// <summary>What the clipboard helper answers.</summary>
    public bool ClipboardAccepts { get; set; } = true;

    /// <summary>A context with the Skills services registered and the clipboard stubbed.</summary>
    public MudBunitContext NewContext()
    {
        MudBunitContext ctx = new();
        SkillsTestServices.AddFromHost(ctx.Services, this.Host);
        ctx.Module(JsModule.Clipboard).Setup<bool>("copy", _ => true).SetResult(this.ClipboardAccepts);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        return ctx;
    }

    /// <summary>A Source, a catalog listing <paramref name="notDownloaded"/> with no folder here, and a Pack whose Skill
    /// <c>docx</c> is downloaded; then a read.</summary>
    public async Task SeedAsync(params string[] notDownloaded)
    {
        this.Host.Source();
        this.Host.Pack("documents", "docx", this.Host.CloneSkill("docx"));
        string catalog = this.Host.Catalog("extra", notDownloaded);
        this.Host.SkillsMd(["packs/documents.skillpack.json"], [catalog]);
        await this.Host.ReadAsync();
    }

    /// <summary>Disposes the host.</summary>
    public void Dispose()
    {
        this.Host.Dispose();
    }
}
