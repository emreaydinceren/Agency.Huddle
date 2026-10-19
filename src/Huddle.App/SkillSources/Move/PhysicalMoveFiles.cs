namespace Agency.Huddle.App.SkillSources.Move;

/// <summary>The production placement: <see cref="File.Move(string, string, bool)"/> after creating the folder.</summary>
internal sealed class PhysicalMoveFiles : IMoveFiles
{
    /// <inheritdoc/>
    public void Place(string from, string to, bool overwrite)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(to) ?? to);
        File.Move(from, to, overwrite);
    }
}
