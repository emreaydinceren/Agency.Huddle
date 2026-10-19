namespace Agency.Huddle.Tests;

/// <summary>Pins <see cref="RepoPaths"/>: the checkout root, joining under it, and resolving data beside the calling source file.</summary>
public sealed class RepoPathsTests
{
    /// <summary>The root is the folder that holds the solution file.</summary>
    [Fact]
    public void Root_ContainsTheSolutionFile()
    {
        Assert.True(File.Exists(Path.Combine(RepoPaths.Root, "Huddle.slnx")));
    }

    /// <summary>Combine joins its segments under the root.</summary>
    [Fact]
    public void Combine_JoinsUnderRoot()
    {
        string combined = RepoPaths.Combine("src", "Huddle.App");

        Assert.Equal(Path.Combine(RepoPaths.Root, "src", "Huddle.App"), combined);
    }

    /// <summary>Data resolves a relative path against the folder of the calling source file.</summary>
    [Fact]
    public void Data_ResolvesBesideTheCallingSourceFile()
    {
        string resolved = RepoPaths.Data("RepoPathsTests.cs");

        Assert.True(File.Exists(resolved));
        Assert.Equal(ThisFile(), resolved);
    }

    /// <summary>The path of this source file.</summary>
    /// <param name="callerFilePath">Filled in by the compiler.</param>
    /// <returns>This file's path.</returns>
    private static string ThisFile([System.Runtime.CompilerServices.CallerFilePath] string callerFilePath = "") => callerFilePath;
}
