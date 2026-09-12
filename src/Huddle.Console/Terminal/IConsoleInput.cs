namespace Agency.Huddle.Console.Terminal;

using System.Threading;
using System.Threading.Tasks;

/// <summary>A seam over console input so tests can script a sequence of lines instead of touching a real console.</summary>
internal interface IConsoleInput
{
    /// <summary>Reads the next line, or <see langword="null"/> at end of input.</summary>
    Task<string?> ReadLineAsync(CancellationToken cancellationToken);

    /// <summary>Drops anything already buffered without blocking.</summary>
    void DiscardPending();
}
