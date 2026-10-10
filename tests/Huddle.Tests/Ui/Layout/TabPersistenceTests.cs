using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Layout;
using Agency.Huddle.App.Tabs;

namespace Agency.Huddle.Tests.Ui.Layout;

/// <summary>Pins <see cref="TabPersistence"/>: stored Tabs come back after the first render, the current address wins, and bad storage is ignored.</summary>
public sealed class TabPersistenceTests
{
    private static string StoredJson()
    {
        TabService source = new();
        Tab a = source.Open("/rooms/ab", "Design");
        source.Pin(a.Id);
        Tab b = source.Open("/teammates");
        source.Pin(b.Id);
        return source.Export().ToJson();
    }

    private static MudBunitContext NewContext(string? stored, out TabService tabs)
    {
        MudBunitContext ctx = new();
        tabs = new TabService();
        ctx.Services.AddSingleton(tabs);
        ctx.JSInterop.Setup<string?>("huddleStorage.get", "huddle:tabs").SetResult(stored);
        ctx.JSInterop.SetupVoid("huddleStorage.set", _ => true).SetVoidResult();
        return ctx;
    }

    /// <summary>Stored Tabs are restored, and the Tab for the current address is the active one.</summary>
    [Fact]
    public async Task FirstRender_RestoresStoredTabs_AndTheCurrentAddressIsActive()
    {
        await using MudBunitContext ctx = NewContext(StoredJson(), out TabService tabs);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/rooms/ab");

        var cut = ctx.Render<TabPersistence>();

        cut.WaitForAssertion(() => Assert.Equal(["room:ab", "teammates"], tabs.Tabs.Select(static t => t.Key)));
        Assert.Equal("room:ab", tabs.Active?.Key);
        Assert.Equal("Design", tabs.Tabs[0].Title);
    }

    /// <summary>Storage that holds nothing usable leaves the Tabs as they were and does not throw.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("not json")]
    [InlineData("{\"version\":9,\"root\":{\"kind\":\"group\",\"tabs\":[]}}")]
    public async Task FirstRender_UnusableStorage_KeepsTheCurrentTabs(string? stored)
    {
        await using MudBunitContext ctx = NewContext(stored, out TabService tabs);
        tabs.Open("/settings");

        var cut = ctx.Render<TabPersistence>();

        cut.WaitForAssertion(() => Assert.Equal("settings", Assert.Single(tabs.Tabs).Key));
    }

    /// <summary>After the restore, every change is written back under the same key.</summary>
    [Fact]
    public async Task AfterRestore_ChangesAreSaved()
    {
        await using MudBunitContext ctx = NewContext(StoredJson(), out TabService tabs);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/rooms/ab");
        var cut = ctx.Render<TabPersistence>();

        // The first save happens only after the restore and the subscription, so it marks "now watching".
        cut.WaitForAssertion(() => ctx.JSInterop.VerifyInvoke("huddleStorage.set"));
        Assert.Equal(2, tabs.Tabs.Count);

        await cut.InvokeAsync(() => tabs.Open("/settings"));

        cut.WaitForAssertion(() =>
        {
            var saves = ctx.JSInterop.Invocations["huddleStorage.set"];
            Assert.Equal("huddle:tabs", saves[^1].Arguments[0]);
            Assert.Contains("/settings", (string?)saves[^1].Arguments[1], StringComparison.Ordinal);
        });
    }
}
