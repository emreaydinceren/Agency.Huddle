using Agency.Huddle.App.FileChanges;

namespace Agency.Huddle.Tests.FileChanges;

/// <summary>
/// Pins <see cref="TouchedPaths.From"/>: adapter-agnostic path extraction out of a tool call's raw
/// input JSON, per FC §6.8 and FC D-3b.
/// </summary>
public sealed class TouchedPathsTests
{
    /// <summary>A single <c>file_path</c> value is returned as its <see cref="Path.GetFullPath(string)"/>.</summary>
    [Fact]
    public void From_FilePathValue_Returned()
    {
        string path = Path.Combine(Path.GetTempPath(), "w", "a.md");
        string json = $$"""{"file_path":{{System.Text.Json.JsonSerializer.Serialize(path)}}}""";

        IReadOnlyList<string> touched = TouchedPaths.From(json);

        string only = Assert.Single(touched);
        Assert.Equal(Path.GetFullPath(path), only);
    }

    /// <summary>Rooted string values nested in an object and inside an array are both returned.</summary>
    [Fact]
    public void From_NestedAndArrayedPaths_AllReturned()
    {
        string first = Path.Combine(Path.GetTempPath(), "w", "a.md");
        string second = Path.Combine(Path.GetTempPath(), "w", "b.md");
        string third = Path.Combine(Path.GetTempPath(), "w", "c.md");
        string json = $$"""
            {
                "file_path": {{System.Text.Json.JsonSerializer.Serialize(first)}},
                "nested": { "target_file": {{System.Text.Json.JsonSerializer.Serialize(second)}} },
                "paths": [ {{System.Text.Json.JsonSerializer.Serialize(third)}} ]
            }
            """;

        IReadOnlyList<string> touched = TouchedPaths.From(json);

        Assert.Equal(3, touched.Count);
        Assert.Contains(Path.GetFullPath(first), touched);
        Assert.Contains(Path.GetFullPath(second), touched);
        Assert.Contains(Path.GetFullPath(third), touched);
    }

    /// <summary>A relative string value is not a path and is ignored.</summary>
    [Fact]
    public void From_RelativeString_Ignored()
    {
        string json = """{"file_path":"relative/a.md"}""";

        IReadOnlyList<string> touched = TouchedPaths.From(json);

        Assert.Empty(touched);
    }

    /// <summary>A string value that is not a rooted path at all is ignored.</summary>
    [Fact]
    public void From_NonPathString_Ignored()
    {
        string json = """{"command":"echo hello"}""";

        IReadOnlyList<string> touched = TouchedPaths.From(json);

        Assert.Empty(touched);
    }

    /// <summary>A <see langword="null"/> raw input gives an empty result.</summary>
    [Fact]
    public void From_Null_Empty()
    {
        IReadOnlyList<string> touched = TouchedPaths.From(null);

        Assert.Empty(touched);
    }

    /// <summary>Malformed JSON gives an empty result rather than throwing.</summary>
    [Fact]
    public void From_MalformedJson_Empty()
    {
        IReadOnlyList<string> touched = TouchedPaths.From("{not json");

        Assert.Empty(touched);
    }

    /// <summary>The same rooted path appearing twice is returned once.</summary>
    [Fact]
    public void From_DuplicatePaths_ReturnedOnce()
    {
        string path = Path.Combine(Path.GetTempPath(), "w", "a.md");
        string json = $$"""
            {
                "file_path": {{System.Text.Json.JsonSerializer.Serialize(path)}},
                "again": {{System.Text.Json.JsonSerializer.Serialize(path)}}
            }
            """;

        IReadOnlyList<string> touched = TouchedPaths.From(json);

        string only = Assert.Single(touched);
        Assert.Equal(Path.GetFullPath(path), only);
    }
}
