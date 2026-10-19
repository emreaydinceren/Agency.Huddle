namespace Agency.Huddle.App.Library;

/// <summary>Why an image that could have been a block was not sent. Never set for an image the design excludes by rule.</summary>
public enum PromptBlockWithheld
{
    /// <summary>Nothing was withheld.</summary>
    None,

    /// <summary>The Turn already held <see cref="PromptDelivery.MaxImagesPerTurn"/> images.</summary>
    ImageCountCap,

    /// <summary>Adding this image would pass <see cref="PromptDelivery.MaxImageBytesPerTurn"/>.</summary>
    TurnBytesCap,

    /// <summary>The reader refused: not a supported raster image.</summary>
    NotAnImage,

    /// <summary>The reader refused: over the per-image byte cap.</summary>
    TooLarge,

    /// <summary>The reader refused: over the edge cap.</summary>
    TooManyPixels,

    /// <summary>The reader refused: the file could not be read.</summary>
    Unreadable,
}
