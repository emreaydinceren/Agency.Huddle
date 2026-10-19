namespace Agency.Huddle.App.Components.Shared;

/// <summary>One chip of a picker cloud: its label and whether it is currently selected.</summary>
/// <param name="Text">The chip's label.</param>
/// <param name="Selected">True when the chip is switched on.</param>
internal sealed record ChipItem(string Text, bool Selected);
