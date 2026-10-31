using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests;

/// <summary>
/// Pins <see cref="TempDataDir"/>'s cleanup. <see cref="TempDataDir.Dispose"/> swallows delete
/// failures, so a pooled SQLite connection that keeps <c>team.db</c> locked would leak the directory
/// silently - and leave every connection to be closed at process exit, which the xunit runner reports
/// as leftover foreground threads.
/// </summary>
public sealed class TempDataDirTests
{
    /// <summary>
    /// A directory whose <c>team.db</c> was opened through a product store is really gone after
    /// <see cref="TempDataDir.Dispose"/>. Each store builds its own connection string, so each is
    /// covered: a store that stopped using the shared <c>Data Source={path}</c> form would no longer
    /// share the pool the cleanup clears.
    /// </summary>
    [Theory]
    [InlineData("SqliteTeamDirectory")]
    [InlineData("PersonaModelStore")]
    [InlineData("PersonaEffortStore")]
    [InlineData("TaskIdAllocator")]
    public async Task Dispose_AfterAStoreOpenedTheDatabase_RemovesTheDirectory(string store)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TempDataDir dir = new();
        string path = dir.Path;

        switch (store)
        {
            case "SqliteTeamDirectory":
                SqliteTeamDirectory directory = new(dir.Options());
                await directory.InitializeAsync("You", ct);
                _ = await directory.GetHumanAsync(ct);
                break;
            case "PersonaModelStore":
                PersonaModelStore models = new(dir.Options());
                models.Set("nova", "model-a");
                Assert.Equal("model-a", models.Get("nova"));
                break;
            case "PersonaEffortStore":
                PersonaEffortStore efforts = new(dir.Options());
                efforts.Set("nova", "high");
                Assert.Equal("high", efforts.Get("nova"));
                break;
            default:
                TaskIdAllocator allocator = new(dir.Options());
                Assert.Equal("PLAT", allocator.PrefixFor("Platform"));
                break;
        }

        Assert.True(File.Exists(System.IO.Path.Combine(path, "team.db")));

        dir.Dispose();

        Assert.False(Directory.Exists(path));
    }
}