using Agency.Huddle.SkillPacks.Folders;
using Agency.Huddle.App.Components.Settings;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Skills;
using Agency.Huddle.App.Skills.Ui;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The provenance line shown wherever a Skill appears (SK-8, SK-9): publisher, repo link, short commit and
/// license for an imported Skill, a "modified locally" chip after a local edit, and "Built-in" or "Yours"
/// with nothing else for a Skill that was not imported.
/// </summary>
public sealed class SkillProvenanceLineTests
{
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    private static Skill Imported(string license, bool modified = false) => new(
        "alpha",
        "Alpha.",
        [],
        ["SKILL.md"],
        SkillSource.Yours,
        null,
        new SkillProvenance(
            "https://github.com/acme/skills",
            "skills/alpha",
            Commit,
            "abc",
            "Acme",
            license,
            new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero)),
        modified);

    private static Skill Plain(SkillSource source) => new("beta", "Beta.", [], ["SKILL.md"], source, null);

    private static IRenderedComponent<SkillProvenanceLine> Render(MudBunitContext ctx, Skill skill) =>
        ctx.Render<SkillProvenanceLine>(p => p.Add(x => x.Skill, skill));

    /// <summary>An imported Skill shows publisher, a repo link with the repo URL as its href, the short commit and the license.</summary>
    [Fact]
    public async Task Render_ImportedSkill_ShowsPublisherRepoCommitAndLicense()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<SkillProvenanceLine> cut = Render(ctx, Imported("MIT"));

        Assert.Equal("Acme", cut.Find(".skill-provenance-publisher").TextContent.Trim());
        Assert.Equal("github.com/acme/skills", cut.Find("a.skill-provenance-repo").TextContent.Trim());
        Assert.Equal("https://github.com/acme/skills", cut.Find("a.skill-provenance-repo").GetAttribute("href"));
        Assert.Equal("0123456", cut.Find(".skill-provenance-commit").TextContent.Trim());
        Assert.Equal("MIT", cut.Find(".skill-provenance-license").TextContent.Trim());
        Assert.Empty(cut.FindAll(".skill-provenance-origin"));
        Assert.Empty(cut.FindAll(".skill-provenance-modified"));
    }

    /// <summary>A license of "none" or blank is stated plainly as "No license".</summary>
    [Theory]
    [InlineData("none")]
    [InlineData("None")]
    [InlineData("")]
    public async Task Render_NoLicense_SaysSo(string license)
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<SkillProvenanceLine> cut = Render(ctx, Imported(license));

        Assert.Equal("No license", cut.Find(".skill-provenance-license").TextContent.Trim());
    }

    /// <summary>A repo URL that is not http or https is shown as text and never becomes a link.</summary>
    [Fact]
    public async Task Render_NonHttpRepo_IsNotALink()
    {
        await using MudBunitContext ctx = new();
        Skill skill = Imported("MIT") with
        {
            Provenance = new SkillProvenance("javascript:alert(1)", "p", Commit, "abc", "Acme", "MIT", DateTimeOffset.UnixEpoch),
        };

        IRenderedComponent<SkillProvenanceLine> cut = Render(ctx, skill);

        Assert.Empty(cut.FindAll("a"));
        Assert.Equal("javascript:alert(1)", cut.Find(".skill-provenance-repo").TextContent.Trim());
    }

    /// <summary>A Skill edited after import shows the "modified locally" chip.</summary>
    [Fact]
    public async Task Render_ModifiedLocally_ShowsChip()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<SkillProvenanceLine> cut = Render(ctx, Imported("MIT", modified: true));

        Assert.Equal("modified locally", cut.Find(".skill-provenance-modified").TextContent.Trim());
    }

    /// <summary>A shipped Skill, with or without an override on disk, reads "Built-in" and shows no provenance facts.</summary>
    [Theory]
    [InlineData("Default")]
    [InlineData("Overridden")]
    public async Task Render_BuiltIn_ShowsBuiltInOnly(string source)
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<SkillProvenanceLine> cut = Render(ctx, Plain(Enum.Parse<SkillSource>(source)));

        Assert.Equal("Built-in", cut.Find(".skill-provenance-origin").TextContent.Trim());
        Assert.Empty(cut.FindAll(".skill-provenance-publisher"));
        Assert.Empty(cut.FindAll(".skill-provenance-license"));
        Assert.Empty(cut.FindAll(".skill-provenance-modified"));
    }

    /// <summary>A Skill the Human wrote reads "Yours" and nothing else.</summary>
    [Fact]
    public async Task Render_Yours_ShowsYoursOnly()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<SkillProvenanceLine> cut = Render(ctx, Plain(SkillSource.Yours));

        Assert.Equal("Yours", cut.Find(".skill-provenance-origin").TextContent.Trim());
        Assert.Empty(cut.FindAll(".skill-provenance-commit"));
    }

    /// <summary>The Skills panel row of an imported Skill carries its provenance line.</summary>
    [Fact]
    public async Task SkillsPanel_ImportedRow_ShowsProvenanceLine()
    {
        using TempDataDir dir = new();
        string folder = Path.Combine(dir.Path, "Skills", "imported-skill");
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            Path.Combine(folder, "SKILL.md"),
            "---\nname: imported-skill\ndescription: An imported skill.\n---\n\n# Imported\n");
        new SkillProvenance(
            "https://github.com/acme/skills",
            "skills/imported-skill",
            Commit,
            SkillFolderHash.ComputeFolder(folder),
            "Acme",
            "MIT",
            new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero)).Write(folder);
        using SkillStore store = new(dir.Options(), NullLogger<SkillStore>.Instance);
        using Agency.Huddle.Tests.SkillPacks.RegistryService.RegistryHost host = new();
        await using MudBunitContext ctx = new();
        SkillsTestServices.AddFromHost(ctx.Services, host);
        ctx.Services.AddSingleton(store);

        IRenderedComponent<SkillsPanel> cut = ctx.Render<SkillsPanel>();

        AngleSharp.Dom.IElement row = cut.FindAll(".skills-grid-row").First(static candidate => candidate.QuerySelector("[data-skill='imported-skill']") is not null);
        Assert.Equal("Acme", row.QuerySelector(".skills-origin")?.TextContent.Trim());
        Assert.Empty(cut.FindAll(".skill-provenance-commit"));
    }

    /// <summary>The grid's Source cell names the publisher only; the repository, short commit and licence go to the tooltip, and a missing publisher falls back to the repository (#739).</summary>
    [Fact]
    public void SkillOriginText_ShortNameAndTooltipDetail()
    {
        Assert.Equal("Anthropic", SkillOriginText.Short("Anthropic", "https://github.com/anthropics/skills/tree/683bc88aaaa/skills/x"));
        Assert.Equal("anthropics/skills", SkillOriginText.Short(null, "https://github.com/anthropics/skills/tree/683bc88aaaa/skills/x"));
        Assert.Equal("anthropics/skills · 683bc88 · Apache-2.0", SkillOriginText.Detail("anthropics/skills", "683bc88", "Apache-2.0"));
        Assert.Equal("anthropics/skills", SkillOriginText.Detail("anthropics/skills", string.Empty, null));
    }
}
