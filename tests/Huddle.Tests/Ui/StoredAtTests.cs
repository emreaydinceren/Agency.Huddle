using Bunit;
using Agency.Huddle.App.Components.Settings;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Tests for <see cref="StoredAt"/>, the "stored at" line under a Settings panel's intro (UX-012, UX-R17).</summary>
public sealed class StoredAtTests
{
    /// <summary>By default the line reads "Stored at" and the path, with the path in code type.</summary>
    [Fact]
    public async Task Default_ReadsStoredAtThenThePathInCode()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<StoredAt>(parameters => parameters
            .Add(p => p.Path, "/data/prompts.json"));

        var line = cut.Find("p.settings-stored-at");
        Assert.Equal("Stored at /data/prompts.json", line.TextContent.Trim());
        Assert.Equal("/data/prompts.json", line.QuerySelector("code")?.TextContent);
    }

    /// <summary>A caller with two stored things (Personas) passes its own words before the path.</summary>
    [Fact]
    public async Task Label_ReplacesTheLeadingWords()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<StoredAt>(parameters => parameters
            .Add(p => p.Label, "Room database stored at")
            .Add(p => p.Path, "/data/team.db"));

        Assert.Equal("Room database stored at /data/team.db", cut.Find("p.settings-stored-at").TextContent.Trim());
    }
}
