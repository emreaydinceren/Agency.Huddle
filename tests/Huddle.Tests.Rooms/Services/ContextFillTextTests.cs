using Agency.Huddle.App.Services;

namespace Agency.Huddle.Tests.Services;

/// <summary>Tests for the model note <see cref="ContextFillText"/> adds to a reading's tooltip.</summary>
public sealed class ContextFillTextTests
{
    /// <summary>A reading that names a model gets the existing tooltip followed by the Answered-by sentence.</summary>
    [Fact]
    public void Tooltip_WithModel_AppendsAnsweredBy()
    {
        ContextFillReading reading = new(50_000, 200_000, Shared: false, "claude-haiku-5-5");

        string tooltip = ContextFillText.Tooltip(reading, "nova");

        Assert.Equal("50,000 of 200,000 tokens used in this Room Answered by claude-haiku-5-5.", tooltip);
    }

    /// <summary>A reading without a model keeps the tooltip text it had before.</summary>
    [Fact]
    public void Tooltip_NoModel_Unchanged()
    {
        ContextFillReading reading = new(50_000, 200_000, Shared: false);

        string tooltip = ContextFillText.Tooltip(reading, "nova");

        Assert.Equal("50,000 of 200,000 tokens used in this Room", tooltip);
    }

    /// <summary>The percentage label is the same with or without a model.</summary>
    [Fact]
    public void Label_WithModel_Unchanged()
    {
        ContextFillReading withModel = new(50_000, 200_000, Shared: false, "claude-haiku-5-5");
        ContextFillReading withoutModel = new(50_000, 200_000, Shared: false);

        Assert.Equal("25%", ContextFillText.Label(withModel));
        Assert.Equal(ContextFillText.Label(withoutModel), ContextFillText.Label(withModel));
    }
}
