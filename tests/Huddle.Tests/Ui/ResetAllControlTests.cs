using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Agency.Huddle.App.Components.Settings;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Renders <see cref="ResetAllControl"/> on its own from its parameters, the same
/// <c>HtmlRenderer</c> + <c>ParameterView.FromDictionary</c> approach <see cref="TeammateCardTests"/>
/// uses for <c>TeammateCard</c>'s <c>ConfirmingRemove</c> state: <c>HtmlRenderer</c> cannot dispatch a
/// click, so the plain-button and confirm-step renderings are exercised directly through the
/// <c>Confirming</c> and <c>AnyModified</c> parameters rather than through an actual click.
/// </summary>
public sealed class ResetAllControlTests
{
    /// <summary>With nothing modified and not confirming, the plain button renders disabled.</summary>
    [Fact]
    public async Task NothingModified_NotConfirming_ButtonIsDisabled()
    {
        var html = await RenderAsync(anyModified: false, confirming: false);

        Assert.Contains("Reset all to defaults", html, StringComparison.Ordinal);
        Assert.Contains("disabled", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Yes, reset everything", html, StringComparison.Ordinal);
    }

    /// <summary>With something modified and not confirming, the plain button renders enabled.</summary>
    [Fact]
    public async Task SomethingModified_NotConfirming_ButtonIsEnabled()
    {
        var html = await RenderAsync(anyModified: true, confirming: false);

        Assert.Contains("Reset all to defaults", html, StringComparison.Ordinal);
        Assert.DoesNotContain("disabled", html, StringComparison.Ordinal);
    }

    /// <summary>While confirming, the plain button is replaced by Confirm/Cancel, never shown alongside it.</summary>
    [Fact]
    public async Task Confirming_ShowsConfirmAndCancelInsteadOfThePlainButton()
    {
        var html = await RenderAsync(anyModified: true, confirming: true);

        Assert.Contains("Yes, reset everything", html, StringComparison.Ordinal);
        Assert.Contains(">Cancel<", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Reset all to defaults", html, StringComparison.Ordinal);
    }

    private static async Task<string> RenderAsync(bool anyModified, bool confirming)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();

        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<ResetAllControl>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    ["AnyModified"] = anyModified,
                    ["Confirming"] = confirming,
                }));

            return output.ToHtmlString();
        });
    }
}
