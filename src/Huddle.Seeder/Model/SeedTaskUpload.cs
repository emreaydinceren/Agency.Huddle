namespace Agency.Huddle.Seeder.Model;

/// <summary>A file the Human uploaded to a Task, saved where the Task's upload flow would save it: <c>Raw/Tasks/&lt;ID&gt;/</c>.</summary>
/// <param name="TaskId">The Task the file is attached to.</param>
/// <param name="FileName">The file name.</param>
/// <param name="Content">The bytes.</param>
internal sealed record SeedTaskUpload(string TaskId, string FileName, byte[] Content)
{
    /// <summary>The path below the data folder, using <c>/</c>; a <see cref="SeedTask.Attachments"/> entry names it.</summary>
    internal string RelativePath => SeedTaskUpload.PathFor(this.TaskId, this.FileName);

    /// <summary>The path below the data folder of a file uploaded to a Task, mirroring <c>TaskUploadFolders</c>.</summary>
    /// <param name="taskId">The Task id.</param>
    /// <param name="fileName">The file name.</param>
    /// <returns>A path such as <c>Raw/Tasks/GROW-0002/mockup.png</c>.</returns>
    internal static string PathFor(string taskId, string fileName) => $"Raw/Tasks/{taskId}/{fileName}";
}
