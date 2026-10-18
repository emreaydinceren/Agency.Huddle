using System.Text.Json;

namespace Agency.Huddle.Tests;

/// <summary>Reads the xunit runner settings that sit next to the running test assembly.</summary>
internal static class RunnerConfig
{
    /// <summary>Reads <c>maxParallelThreads</c> from <c>xunit.runner.json</c> in the output folder.</summary>
    /// <returns>The value as text (for example <c>2</c> or <c>0.5x</c>), or <see langword="null"/> when the file or key is missing.</returns>
    internal static string? MaxParallelThreads()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "xunit.runner.json");
        if (!File.Exists(path))
        {
            return null;
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("maxParallelThreads", out JsonElement value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
    }
}
