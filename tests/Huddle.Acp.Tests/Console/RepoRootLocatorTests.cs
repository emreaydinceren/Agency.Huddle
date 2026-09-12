namespace Agency.Huddle.Acp.Tests.Console;

using System;
using System.IO;
using Agency.Huddle.Console.Configuration;
using Xunit;

public sealed class RepoRootLocatorTests : IDisposable
{
    private readonly string tempDirectory;

    public RepoRootLocatorTests()
    {
        this.tempDirectory = Path.Combine(Path.GetTempPath(), "team-acp-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this.tempDirectory);
    }

    [Fact]
    public void Locate_FindsDirectoryContainingTeamSlnx()
    {
        File.WriteAllText(Path.Combine(this.tempDirectory, "Huddle.slnx"), string.Empty);
        string startDirectory = Path.Combine(this.tempDirectory, "a", "b");
        Directory.CreateDirectory(startDirectory);

        string? result = RepoRootLocator.Locate(startDirectory);

        Assert.Equal(this.tempDirectory, result);
    }

    [Fact]
    public void Locate_NotFound_ReturnsNull()
    {
        string startDirectory = Path.Combine(this.tempDirectory, "a", "b");
        Directory.CreateDirectory(startDirectory);

        string? result = RepoRootLocator.Locate(startDirectory);

        Assert.Null(result);
    }

    public void Dispose()
    {
        Directory.Delete(this.tempDirectory, recursive: true);
    }
}
