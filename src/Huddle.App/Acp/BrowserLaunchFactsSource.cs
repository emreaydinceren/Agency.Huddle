using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Acp;

/// <summary>Computes the <see cref="BrowserLaunchFacts"/> once, on first use, for the running app.</summary>
internal sealed class BrowserLaunchFactsSource
{
    private readonly Lazy<BrowserLaunchFacts> facts;

    /// <summary>Creates the source.</summary>
    /// <param name="options">The bound <c>Team</c> options.</param>
    public BrowserLaunchFactsSource(IOptions<TeamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.facts = new Lazy<BrowserLaunchFacts>(
            () => BrowserServerLocator.Locate(options.Value.Browser, AppContext.BaseDirectory, null));
    }

    /// <summary>Creates a source that always answers with the given facts, for a test that must not depend on what is installed.</summary>
    /// <param name="facts">The facts to answer with.</param>
    internal BrowserLaunchFactsSource(BrowserLaunchFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        this.facts = new Lazy<BrowserLaunchFacts>(() => facts);
    }

    /// <summary>The facts, computed on the first read.</summary>
    public BrowserLaunchFacts Current => this.facts.Value;
}
