namespace Agency.Huddle.App.Components.Library;

/// <summary>The header's mode toggle for an editable Markdown document (Task 12.2, Spec §8's Toggle Group row).</summary>
public enum LibraryMode
{
    /// <summary>The document renders as read-only HTML.</summary>
    Read,

    /// <summary>The document opens in the CodeMirror editor.</summary>
    Edit,

    /// <summary>The rendered view and the editor show side by side.</summary>
    Split,
}
