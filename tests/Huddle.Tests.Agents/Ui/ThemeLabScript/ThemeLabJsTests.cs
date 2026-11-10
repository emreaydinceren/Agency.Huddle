using System.Diagnostics;
using System.Text.Json;

namespace Agency.Huddle.Tests.Ui.ThemeLabScript;

/// <summary>
/// Runs <c>theme-lab.js</c> under <c>node</c> against a hand-made DOM (<c>theme-lab-harness.mjs</c>), so the Inspect walk
/// over the stylesheets and the preview's apply and reset are tested as behaviour. Needs <c>node</c> on PATH, like the
/// other node-backed tests.
/// </summary>
public sealed class ThemeLabJsTests
{
    private static JsonElement Run(string scenario)
    {
        string script = RepoPaths.Data("theme-lab-harness.mjs");
        ProcessStartInfo info = new("node")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        info.ArgumentList.Add(script);
        info.ArgumentList.Add(scenario);
        info.ArgumentList.Add(CssSource.RepoPath());
        using Process process = Process.Start(info) ?? throw new InvalidOperationException("node did not start.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, error);
        return JsonDocument.Parse(output).RootElement.Clone();
    }

    /// <summary>Only rules that match the element and read a <c>--hu-*</c> token in a property are reported, through nested at-rules and past an unreadable sheet.</summary>
    [Fact]
    public void TokensReadBy_ReportsMatchingRulesThatReadSkinTokens()
    {
        JsonElement found = Run("tokens-read-by").GetProperty("found");

        Assert.Equal(3, found.GetArrayLength());
        Assert.Equal("background", found[0].GetProperty("property").GetString());
        Assert.Equal("--hu-ground-raised", found[0].GetProperty("tokens")[0].GetString());
        Assert.Equal("border", found[1].GetProperty("property").GetString());
        Assert.Equal("box-shadow", found[2].GetProperty("property").GetString());
        Assert.Equal(1, found[2].GetProperty("tokens").GetArrayLength());
    }

    /// <summary>The inspector text names the element and its properties, or says it reads no token.</summary>
    [Fact]
    public void Describe_NamesTheElementAndItsTokens()
    {
        JsonElement result = Run("tokens-read-by");

        Assert.Equal(
            "div#x\nbackground <- --hu-ground-raised\nborder <- --ink-line\nbox-shadow <- --hu-shadow-card",
            result.GetProperty("described").GetString());
        Assert.Equal("p: reads no skin token", result.GetProperty("none").GetString());
    }

    /// <summary>Apply replaces the style block and sets the palette variables; reset restores the original text and removes them.</summary>
    [Fact]
    public void ApplyThenReset_RestoresThePage()
    {
        JsonElement result = Run("apply-reset");

        Assert.Equal(":root{--hu-a:2}", result.GetProperty("during").GetProperty("text").GetString());
        Assert.Equal("#654321", result.GetProperty("during").GetProperty("props").GetProperty("--mud-palette-primary").GetString());
        Assert.Equal("ORIGINAL", result.GetProperty("after").GetProperty("text").GetString());
        Assert.Empty(result.GetProperty("after").GetProperty("props").EnumerateObject());
    }
}
