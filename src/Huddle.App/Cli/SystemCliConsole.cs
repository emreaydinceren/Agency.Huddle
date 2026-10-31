namespace Agency.Huddle.App.Cli;

/// <summary>The process console as an <see cref="ICliConsole"/>.</summary>
internal sealed class SystemCliConsole : ICliConsole
{
    /// <inheritdoc />
    public TextWriter Out => Console.Out;

    /// <inheritdoc />
    public TextWriter Error => Console.Error;

    /// <inheritdoc />
    public bool IsOutputRedirected => Console.IsOutputRedirected;
}
