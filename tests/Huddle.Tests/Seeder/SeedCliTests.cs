using Microsoft.Extensions.Time.Testing;
using Agency.Huddle.Seeder;

namespace Agency.Huddle.Tests.Seeder;

/// <summary>The command line: defaults, errors, and one full run through the real wipe guard.</summary>
public sealed class SeedCliTests
{
    private static readonly FakeTimeProvider Clock = new(new DateTimeOffset(2026, 9, 29, 23, 30, 0, TimeSpan.Zero));

    /// <summary>With no arguments the default scenario is built into the default root, dated by the clock's UTC day.</summary>
    [Fact]
    public void Parse_NoArguments_UsesDefaults()
    {
        (SeedRequest? request, string? message, int exitCode) = SeedCli.Parse([], Clock);

        Assert.Null(message);
        Assert.Equal(0, exitCode);
        Assert.NotNull(request);
        Assert.Equal("software-co", request.Scenario.Name);
        Assert.Equal(new DateOnly(2026, 9, 29), request.Today);
        Assert.Equal(SeedCli.DefaultRoot("software-co"), request.Root);
    }

    /// <summary>Every option overrides its default.</summary>
    [Fact]
    public void Parse_AllOptions_AreApplied()
    {
        (SeedRequest? request, _, int exitCode) = SeedCli.Parse(["--scenario", "SOFTWARE-CO", "--root", "D:\\x\\y", "--today", "2027-01-31"], Clock);

        Assert.Equal(0, exitCode);
        Assert.NotNull(request);
        Assert.Equal("D:\\x\\y", request.Root);
        Assert.Equal(new DateOnly(2027, 1, 31), request.Today);
    }

    /// <summary>Bad input is exit code 2 with a message, and never a request.</summary>
    [Theory]
    [InlineData("--scenario", "nope")]
    [InlineData("--today", "31/01/2027")]
    [InlineData("--wat", "x")]
    [InlineData("--root")]
    public void Parse_BadInput_ReturnsExitCodeTwo(params string[] args)
    {
        (SeedRequest? request, string? message, int exitCode) = SeedCli.Parse(args, Clock);

        Assert.Null(request);
        Assert.NotNull(message);
        Assert.Equal(2, exitCode);
    }

    /// <summary>Help exits successfully and explains the wipe.</summary>
    [Fact]
    public void Parse_Help_ExitsZeroAndMentionsTheWipe()
    {
        (SeedRequest? request, string? message, int exitCode) = SeedCli.Parse(["--help"], Clock);

        Assert.Null(request);
        Assert.Equal(0, exitCode);
        Assert.Contains(".huddle-seed", message, StringComparison.Ordinal);
    }

    /// <summary>A refused root ends with exit code 1, an explanation, and nothing deleted.</summary>
    [Fact]
    public async Task Run_RefusedRoot_ExitsOneAndLeavesTheFolderAlone()
    {
        string root = Path.Combine(Path.GetTempPath(), "seed-cli-tests", Guid.NewGuid().ToString("N"), "theirs");
        Directory.CreateDirectory(root);
        string file = Path.Combine(root, "precious.txt");
        await File.WriteAllTextAsync(file, "keep", TestContext.Current.CancellationToken);
        StringWriter output = new();
        StringWriter error = new();
        try
        {
            int exit = await SeedCli.RunAsync(["--root", root], output, error, Clock, TestContext.Current.CancellationToken);

            Assert.Equal(1, exit);
            Assert.Contains("Refusing to wipe", error.ToString(), StringComparison.Ordinal);
            Assert.True(File.Exists(file));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(root)!, recursive: true);
        }
    }

    /// <summary>A full run through the real protected-folder list succeeds and prints the launch command.</summary>
    [Fact]
    public async Task Run_FreshRoot_SeedsAndPrintsTheLaunchCommand()
    {
        string root = Path.Combine(Path.GetTempPath(), "seed-cli-tests", Guid.NewGuid().ToString("N"), "root");
        StringWriter output = new();
        StringWriter error = new();
        try
        {
            int exit = await SeedCli.RunAsync(["--root", root], output, error, Clock, TestContext.Current.CancellationToken);

            Assert.Equal(0, exit);
            Assert.Equal(string.Empty, error.ToString());
            Assert.Contains("Seeded 9 Teammates, 5 Teams, 41 Tasks", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("--Team:Acp:Adapters:0:Args:0=--mock", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("--Team:DemoAgent:Enabled=false", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearPool(new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={Path.Combine(root, "data", "team.db")}"));
            Directory.Delete(Path.GetDirectoryName(root)!, recursive: true);
        }
    }
}
