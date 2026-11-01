using System.Globalization;

namespace Agency.Huddle.Tools.AllocReport;

/// <summary>Sampled bytes and sample counts per key, printed as a ranked Markdown block.</summary>
internal sealed class Tally
{
    private readonly Dictionary<string, (long Bytes, int Samples)> entries = new(StringComparer.Ordinal);

    /// <summary>Gets the sum of every sample added.</summary>
    public long TotalBytes { get; private set; }

    /// <summary>Adds one sample of <paramref name="bytes"/> to <paramref name="key"/>.</summary>
    public void Add(string key, long bytes)
    {
        this.entries.TryGetValue(key, out (long Bytes, int Samples) current);
        this.entries[key] = (current.Bytes + bytes, current.Samples + 1);
        this.TotalBytes += bytes;
    }

    /// <summary>Returns the bytes recorded for <paramref name="key"/>, or zero.</summary>
    public long BytesFor(string key) => this.entries.TryGetValue(key, out (long Bytes, int Samples) value) ? value.Bytes : 0;

    /// <summary>Prints the <paramref name="top"/> largest keys with their share of <paramref name="total"/>.</summary>
    public void Print(string title, int top, long total)
    {
        Console.WriteLine();
        Console.WriteLine($"## {title}");
        Console.WriteLine();
        Console.WriteLine("```text");
        Console.WriteLine("        MB      %  samples  item");
        foreach ((string key, (long bytes, int samples)) in this.entries.OrderByDescending(entry => entry.Value.Bytes).Take(top))
        {
            double percent = 100.0 * bytes / Math.Max(1, total);
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024.0 / 1024.0,10:F1} {percent,6:F1} {samples,8}  {key}"));
        }

        Console.WriteLine("```");
    }
}
