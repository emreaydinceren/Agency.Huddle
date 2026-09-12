namespace Agency.Huddle.Console.Configuration;

using System.IO;

internal static class RepoRootLocator
{
    internal static string? Locate(string startDirectory)
    {
        DirectoryInfo? directory = new DirectoryInfo(startDirectory);

        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "Huddle.slnx");
            if (File.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
