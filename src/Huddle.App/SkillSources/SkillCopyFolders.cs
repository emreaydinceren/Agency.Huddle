namespace Agency.Huddle.App.SkillSources;

/// <summary>The copier's two private folders in <c>Skills/</c>: <c>.staging</c>, where a copy is made, and <c>.trash</c>, where
/// the old copy waits while the new one is swapped in (§8.6). Neither is ever read as a Skill.</summary>
internal static class SkillCopyFolders
{
    /// <summary>The staging folder's name.</summary>
    public const string Staging = ".staging";

    /// <summary>The trash folder's name.</summary>
    public const string Trash = ".trash";

    /// <summary>At start-up (§8.6 step 7): any <c>.trash/&lt;name&gt;-*</c> whose <c>Skills/&lt;name&gt;/</c> is missing (a crash
    /// between the two renames) is moved back first; then <c>.trash</c> and <c>.staging</c> are emptied.</summary>
    /// <returns>The names put back.</returns>
    public static IReadOnlyList<string> Repair(string skillsDir, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(skillsDir);
        ArgumentNullException.ThrowIfNull(logger);
        List<string> restored = [];
        string trash = Path.Combine(skillsDir, Trash);
        if (Directory.Exists(trash))
        {
            foreach (string folder in Directory.GetDirectories(trash).Order(StringComparer.Ordinal))
            {
                string name = NameOf(Path.GetFileName(folder));
                string target = Path.Combine(skillsDir, name);
                if (name.Length > 0 && !Directory.Exists(target))
                {
                    Directory.Move(folder, target);
                    restored.Add(name);
                    logger.LogWarning("Put {Skill} back from Skills/.trash: a copy was interrupted between its two renames.", name);
                }
            }
        }

        Empty(trash, logger);
        Empty(Path.Combine(skillsDir, Staging), logger);
        return restored;
    }

    /// <summary>The Skill name of a <c>&lt;name&gt;-&lt;random&gt;</c> folder: everything before the last hyphen.</summary>
    public static string NameOf(string folderName)
    {
        ArgumentNullException.ThrowIfNull(folderName);
        int hyphen = folderName.LastIndexOf('-');
        return hyphen <= 0 ? string.Empty : folderName[..hyphen];
    }

    /// <summary>Deletes the folder's contents, never following a link; a failure is logged and left for the next start.</summary>
    private static void Empty(string folder, ILogger logger)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        foreach (string child in Directory.GetDirectories(folder))
        {
            try
            {
                if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
                {
                    Directory.Delete(child);
                }
                else
                {
                    Directory.Delete(child, recursive: true);
                }
            }
            catch (IOException exception)
            {
                logger.LogWarning(exception, "Could not empty {Folder}; it is tried again at the next start.", child);
            }
            catch (UnauthorizedAccessException exception)
            {
                logger.LogWarning(exception, "Could not empty {Folder}; it is tried again at the next start.", child);
            }
        }
    }
}
