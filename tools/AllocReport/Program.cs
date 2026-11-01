using System.Globalization;
using Microsoft.Diagnostics.Tracing.Etlx;

namespace Agency.Huddle.Tools.AllocReport;

/// <summary>
/// Ranks the sampled <c>GCAllocationTick</c> events in a <c>gc-verbose</c> trace by type, by the
/// first application frame and by call stack, and prints the result as Markdown.
/// Usage: <c>AllocReport &lt;trace.nettrace&gt; [top=25] [appNamespacePrefix]</c>.
/// </summary>
internal static class Program
{
    private const int MaxFrames = 40;

    private static readonly HashSet<string> StringTypes = new(StringComparer.Ordinal)
    {
        "System.String", "System.Char[]", "System.String[]", "System.Text.StringBuilder", "System.Byte[]",
    };

    /// <summary>Reads the trace and writes the report to standard output.</summary>
    private static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: AllocReport <trace.nettrace> [top=25] [appNamespacePrefix]");
            return 1;
        }

        string tracePath = args[0];
        int top = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 25;
        string? appPrefix = args.Length > 2 ? args[2] : null;

        Tally byType = new();
        Tally stringsByMethod = new();
        Tally stringsByStack = new();

        string etlxPath = TraceLog.CreateFromEventPipeDataFile(tracePath);
        using (TraceLog log = new(etlxPath))
        {
            TraceLogEventSource source = log.Events.GetSource();
            source.Clr.GCAllocationTick += data =>
            {
                string type = string.IsNullOrEmpty(data.TypeName) ? "?" : data.TypeName;
                long bytes = data.AllocationAmount64;
                byType.Add(type, bytes);
                if (!StringTypes.Contains(type))
                {
                    return;
                }

                List<string> frames = ReadFrames(data.CallStack());
                string appFrame = frames.Find(frame => IsAppFrame(frame, appPrefix)) ?? "(no app frame found)";
                stringsByMethod.Add($"{appFrame}  [{type}]", bytes);
                stringsByStack.Add($"[{type}]\n        " + string.Join("\n        ", frames.Take(8)), bytes);
            };
            source.Process();
        }

        long total = byType.TotalBytes;
        Console.WriteLine($"# Allocation report: {Path.GetFileName(tracePath)}");
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Sampled total: {total / 1024.0 / 1024.0:F1} MB (estimates from AllocationTick sampling)"));
        long stringBytes = byType.BytesFor("System.String") + byType.BytesFor("System.Char[]");
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"System.String + System.Char[] share: {100.0 * stringBytes / Math.Max(1, total):F1}%"));
        byType.Print("Allocations by type", top, total);
        stringsByMethod.Print("String-related allocations by first app method", top, total);
        stringsByStack.Print("String-related allocations by call stack (leaf first)", top, total);
        return 0;
    }

    /// <summary>Collects frame names from the allocation site (leaf) toward the root.</summary>
    private static List<string> ReadFrames(TraceCallStack? stack)
    {
        List<string> frames = [];
        for (TraceCallStack? frame = stack; frame is not null && frames.Count < MaxFrames; frame = frame.Caller)
        {
            string name = frame.CodeAddress.FullMethodName;
            if (!string.IsNullOrEmpty(name))
            {
                frames.Add(name);
            }
        }

        return frames;
    }

    /// <summary>True when the frame belongs to the application rather than the runtime or a framework.</summary>
    private static bool IsAppFrame(string method, string? appPrefix) =>
        appPrefix is not null
            ? method.StartsWith(appPrefix, StringComparison.Ordinal)
            : !method.StartsWith("System.", StringComparison.Ordinal)
              && !method.StartsWith("Microsoft.", StringComparison.Ordinal)
              && !method.StartsWith("Interop", StringComparison.Ordinal);
}
