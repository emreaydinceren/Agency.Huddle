namespace Agency.Huddle.App.Library;

/// <summary>
/// The Library Pane's open/closed state and current file, shared by <see cref="Components.Library.LibraryNavLink"/>
/// (the sidebar toggle), <see cref="Components.Library.LibraryPaneHost"/> (the split panel that renders the pane)
/// and any other surface (a <c>?library=</c> navigation) that opens the pane on a file. Scoped per circuit: one
/// pane state per Human session, matching every other per-circuit store in this app.
/// </summary>
internal sealed class LibraryPaneState
{
    /// <summary>Whether the pane is currently open.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>The file currently open in the pane, or <see langword="null"/> when nothing is open yet.</summary>
    public LibraryPath? CurrentFile { get; private set; }

    /// <summary>Raised whenever <see cref="IsOpen"/> or <see cref="CurrentFile"/> changes.</summary>
    public event Action? Changed;

    /// <summary>Opens the pane on <paramref name="path"/> (or leaves it open with no file selected when <see langword="null"/>).</summary>
    /// <param name="path">The file to open, or <see langword="null"/>.</param>
    public void OpenFile(LibraryPath? path)
    {
        this.CurrentFile = path;
        this.IsOpen = true;
        this.Changed?.Invoke();
    }

    /// <summary>Flips <see cref="IsOpen"/>.</summary>
    public void Toggle()
    {
        this.IsOpen = !this.IsOpen;
        this.Changed?.Invoke();
    }

    /// <summary>Closes the pane. <see cref="CurrentFile"/> is left as-is, so reopening returns to the same file.</summary>
    public void Close()
    {
        this.IsOpen = false;
        this.Changed?.Invoke();
    }
}
