namespace Agency.Huddle.App.Tasks;

/// <summary>Configuration for the Tasks feature.</summary>
public sealed class TasksOptions
{
    /// <summary>Whether Tasks are enabled. When false, the UI is hidden, no tools are offered, and no one is woken on changes.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The folder where Task files are stored, relative to DataDir. Defaults to "Tasks".</summary>
    public string Dir { get; set; } = "Tasks";

    /// <summary>Whether wake-up notifications are enabled when Tasks change. When false, Tasks are kept but no one is notified.</summary>
    public bool WakeEnabled { get; set; } = true;

    /// <summary>The number of seconds to wait before coalescing multiple Task changes into a single wake notification. Defaults to 5 seconds.</summary>
    public int WakeCoalesceSeconds { get; set; } = 5;

    /// <summary>The maximum number of wake-up notifications that can be sent for a single Task by agents before pausing. Defaults to 10. A value of 0 or less disables the per-Task budget.</summary>
    public int AgentWakeBudget { get; set; } = 10;
}
