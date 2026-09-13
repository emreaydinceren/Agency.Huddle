using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Proves the composed application can actually construct the services registered in
/// <c>ServiceCollectionExtensions</c>. A registration whose own dependencies are not registered
/// builds perfectly well and fails only when something first resolves it — which, for a service
/// whose consumers arrive in a later change, can be a long way from the commit that broke it.
/// </summary>
public sealed class CompositionTests
{
    /// <summary>
    /// <see cref="PersonaHealth"/> resolves, including the <see cref="TimeProvider"/> it takes.
    /// Nothing else in the application injects a clock, so that registration exists solely for this.
    /// </summary>
    [Fact]
    public async Task PersonaHealth_ResolvesFromTheComposedApplication()
    {
        await using var factory = new TeamWebApplicationFactory();

        var health = factory.Services.GetRequiredService<PersonaHealth>();

        Assert.Empty(health.All);
    }

    /// <summary><see cref="Drafts"/> resolves, and is the same Singleton instance every time.</summary>
    [Fact]
    public async Task Drafts_ResolvesAsASingleton()
    {
        await using var factory = new TeamWebApplicationFactory();

        var first = factory.Services.GetRequiredService<Drafts>();
        var second = factory.Services.GetRequiredService<Drafts>();

        Assert.Same(first, second);
    }
}
