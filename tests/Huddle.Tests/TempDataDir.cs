using Microsoft.Extensions.Options;
using Agency.Huddle.App;

namespace Agency.Huddle.Tests;

/// <summary>
/// Creates an isolated, disposable data directory under the OS temp folder for
/// tests that exercise <see cref="TeamOptions"/>-driven storage (file chat store,
/// SQLite directory, pipe host, web host).
/// </summary>
public sealed class TempDataDir : IDisposable
{
    public TempDataDir()
    {
        this.Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "team-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this.Path);
    }

    public string Path { get; }

    public IOptions<TeamOptions> Options()
    {
        return Microsoft.Extensions.Options.Options.Create(new TeamOptions { DataDir = this.Path });
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(this.Path))
            {
                Directory.Delete(this.Path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}