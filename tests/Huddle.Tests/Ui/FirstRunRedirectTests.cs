using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The first thing on a blank system is the Human's profile: until <c>profile.json</c> exists, opening
/// the app redirects to the Settings Profile tab, which itself is served without a redirect.
/// </summary>
public sealed class FirstRunRedirectTests
{
    /// <summary>With no saved profile, <c>/</c> answers with a redirect to <c>/settings/profile</c>.</summary>
    [Fact]
    public async Task Root_WithNoProfile_RedirectsToTheProfileTab()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new() { SeedProfile = false };
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using HttpResponseMessage response = await client.GetAsync("/", ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        // contains-ok: the Location header may be absolute or base-relative depending on the host; the route is what matters.
        Assert.EndsWith("/settings/profile", response.Headers.Location?.ToString(), StringComparison.Ordinal);
    }

    /// <summary>The Profile tab itself is served on first run, with the welcome line, and never redirects to itself.</summary>
    [Fact]
    public async Task ProfileTab_WithNoProfile_IsServedWithTheWelcome()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new() { SeedProfile = false };
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using HttpResponseMessage response = await client.GetAsync("/settings/profile", ct);
        string html = await response.Content.ReadAsStringAsync(ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // contains-ok: a raw prerendered page; the welcome alert's class proves it rendered, its wording is pinned in the Razor source.
        Assert.Contains("profile-welcome", html, StringComparison.Ordinal);
    }

    /// <summary>Once a profile is saved, <c>/</c> is served as asked.</summary>
    [Fact]
    public async Task Root_WithASavedProfile_IsNotRedirected()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using HttpResponseMessage response = await client.GetAsync("/", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
