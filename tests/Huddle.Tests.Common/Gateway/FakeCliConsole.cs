using Agency.Huddle.App.Cli;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>An <see cref="ICliConsole"/> over two <see cref="StringWriter"/>s, so a test reads what a verb printed.</summary>
internal sealed class FakeCliConsole : ICliConsole, IDisposable
{
    private readonly StringWriter output = new();
    private readonly StringWriter error = new();

    /// <inheritdoc />
    public TextWriter Out => this.output;

    /// <inheritdoc />
    public TextWriter Error => this.error;

    /// <inheritdoc />
    public bool IsOutputRedirected { get; set; }

    /// <summary>Gets everything written to <see cref="Out"/>.</summary>
    public string OutText => this.output.ToString();

    /// <summary>Gets everything written to <see cref="Error"/>.</summary>
    public string ErrorText => this.error.ToString();

    /// <summary>Gets everything written to either writer.</summary>
    public string AllText => this.OutText + this.ErrorText;

    /// <inheritdoc />
    public void Dispose()
    {
        this.output.Dispose();
        this.error.Dispose();
    }
}
