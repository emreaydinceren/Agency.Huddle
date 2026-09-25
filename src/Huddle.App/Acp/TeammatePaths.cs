using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Provides paths for teammate definitions and work directories, abstracting the current layout
/// where definitions are in {DataDir}/{TeamsDir}/{name}.md and work directories are in
/// {DataDir}/{WorkDir}/{name}.
/// </summary>
internal sealed class TeammatePaths(IOptions<TeamOptions> options)
{
    /// <summary>
    /// The root directory containing teammate definitions, derived from
    /// {DataDir}/{Acp:TeamsDir}.
    /// </summary>
    public string DefinitionsRoot => Path.Combine(this.options.Value.DataDir, this.options.Value.Acp.TeamsDir);

    /// <summary>
    /// The root directory containing teammate work directories, derived from
    /// {DataDir}/{Acp:WorkDir}.
    /// </summary>
    public string WorkDirRoot => Path.Combine(this.options.Value.DataDir, this.options.Value.Acp.WorkDir);

    private readonly IOptions<TeamOptions> options = options;

    /// <summary>
    /// Returns the path to a teammate's definition file.
    /// </summary>
    /// <param name="name">The teammate name (not null or whitespace).</param>
    /// <returns>The full path to {DefinitionsRoot}/{name}.md.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="name"/> is empty or whitespace.</exception>
    public string DefinitionFile(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Path.Combine(this.DefinitionsRoot, $"{name}.md");
    }

    /// <summary>
    /// Returns the path to a teammate's work directory.
    /// </summary>
    /// <param name="name">The teammate name (not null or whitespace).</param>
    /// <returns>The full path to {WorkDirRoot}/{name}.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="name"/> is empty or whitespace.</exception>
    public string WorkDir(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Path.Combine(this.WorkDirRoot, name);
    }
}
