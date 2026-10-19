namespace Agency.Huddle.App.SkillSources;

/// <summary>How a path is written for the AI tool that will open it (§6.8, <c>G5b</c>). Public because the Razor components
/// take it as a <c>[Parameter]</c>.</summary>
public enum PathFormat
{
    /// <summary><c>E:\Huddle\App_Data\InstallSkillPack.md</c>.</summary>
    Windows,

    /// <summary><c>/mnt/e/Huddle/App_Data/InstallSkillPack.md</c>, for a tool running in WSL.</summary>
    Wsl,

    /// <summary><c>E:/Huddle/App_Data/InstallSkillPack.md</c>.</summary>
    ForwardSlashes,
}
