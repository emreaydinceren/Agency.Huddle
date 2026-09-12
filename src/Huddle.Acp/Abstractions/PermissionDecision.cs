namespace Agency.Huddle.Acp.Abstractions;

using System;

/// <summary>Base type for a decision made in response to a permission request.</summary>
public abstract record PermissionDecision
{
    public static PermissionDecision Cancelled { get; } = new CancelledDecision();
}

/// <summary>Represents choosing one of the offered permission options.</summary>
public sealed record SelectedDecision : PermissionDecision
{
    public SelectedDecision(string optionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(optionId);
        this.OptionId = optionId;
    }

    public string OptionId { get; }
}

/// <summary>Represents declining to answer a permission request.</summary>
public sealed record CancelledDecision : PermissionDecision;