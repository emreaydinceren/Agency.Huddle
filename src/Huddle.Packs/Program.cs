namespace Agency.Huddle.Packs;

/// <summary>The <c>huddle-packs</c> entry point (§6.4).</summary>
public static class Program
{
    /// <summary>Runs the command with the console's streams and returns its exit code.</summary>
    public static int Main(string[] args)
    {
        return Run(args, Console.Out, Console.Error);
    }

    /// <summary>Runs the command with the given streams; tests call this directly.</summary>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        output.WriteLine("huddle-packs");
        return 0;
    }
}
