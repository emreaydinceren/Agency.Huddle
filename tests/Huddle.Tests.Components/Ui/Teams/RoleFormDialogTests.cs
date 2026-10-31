using Agency.Huddle.App.Components.Teams;
using Agency.Huddle.Tests.Ui.SkillPacks;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>Pins the role form's Skills list (Skill Packs spec §6.10.5): installed and copyable Skills are options, a
/// not-downloaded Skill is listed below, and one the role already names stays checked as Not downloaded.</summary>
public sealed class RoleFormDialogTests : IDisposable
{
    private readonly SkillsUiFixture fixture = new();

    /// <summary>Disposes the fixture.</summary>
    public void Dispose()
    {
        this.fixture.Dispose();
    }

    /// <summary>The select offers installed and copyable Skills, not the whole catalog; a not-downloaded one is listed below;
    /// a not-downloaded Skill the role names stays an option, captioned Not downloaded.</summary>
    [Fact]
    public async Task Skills_InstalledAndCopyable_NotDownloadedBelow_KeptWhenNamed()
    {
        this.fixture.Host.Source();
        string catalog = this.fixture.Host.Catalog("extra", ["copyme", "ghost", "kept"], name => name == "copyme" ? this.fixture.Host.CloneSkill(name) : null);
        this.fixture.Host.SkillsMd([], [catalog]);
        await this.fixture.Host.ReadAsync();
        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        DialogParameters<RoleFormDialog> parameters = new() { { dialog => dialog.Title, "Writer" }, { dialog => dialog.RoleSkills, (IReadOnlyList<string>)["kept"] } };
        await cut.InvokeAsync(() => ctx.Services.GetRequiredService<IDialogService>().ShowAsync<RoleFormDialog>("Edit role", parameters));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".custom-role-skills")));

        await cut.InvokeAsync(() => cut.Find(".custom-role-skills .mud-select-input").MouseDownAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".custom-role-copy-caption")));
        Assert.Equal("Huddle copies it into Skills/ when an Agent in this role is hired.", cut.Find(".custom-role-copy-caption").TextContent.Trim());
        Assert.Equal("Not downloaded", cut.Find(".custom-role-not-downloaded").TextContent.Trim());
        Assert.Equal("Not on this computer yet (2)", cut.Find(".custom-role-not-here-toggle").TextContent.Trim());
        Assert.Equal("Skills not downloaded are listed below; ask your AI tool to fetch them.", cut.Find(".custom-role-skills-helper").TextContent.Trim());
    }
}
