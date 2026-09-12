namespace Agency.Huddle.Acp.Abstractions;

/// <summary>Identifies the category of action a tool call performs.</summary>
public enum ToolKind
{
    Read,
    Edit,
    Delete,
    Move,
    Search,
    Execute,
    Think,
    Fetch,
    SwitchMode,
    Other,
}

/// <summary>Represents the lifecycle state of a tool call.</summary>
public enum ToolCallStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
}

/// <summary>Represents why an agent turn ended.</summary>
public enum StopReason
{
    EndTurn,
    MaxTokens,
    MaxTurnRequests,
    Refusal,
    Cancelled,
}

/// <summary>Identifies the category of a permission option offered to the user.</summary>
public enum PermissionOptionKind
{
    AllowOnce,
    AllowAlways,
    RejectOnce,
    RejectAlways,
}

/// <summary>Represents the completion state of a plan entry.</summary>
public enum PlanEntryStatus
{
    Pending,
    InProgress,
    Completed,
}

/// <summary>Represents the priority of a plan entry.</summary>
public enum PlanEntryPriority
{
    High,
    Medium,
    Low,
}

/// <summary>Selects how a session's system prompt combines with the agent's own.</summary>
public enum SystemPromptMode
{
    Append,
    Replace,
}