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

    /// <summary>
    /// The single Library Root the pane's tree is narrowed to, or <see langword="null"/> for the
    /// whole Library (every root). Set by the sidebar's per-root rows through <see cref="OpenRoot"/>;
    /// cleared by <see cref="OpenFile"/> since a file can come from anywhere (a wikilink, a chat
    /// <c>?library=</c> link) and must never be hidden by a scope the Human did not choose for it.
    /// </summary>
    public LibraryLocation? Scope { get; private set; }

    /// <summary>Raised whenever <see cref="IsOpen"/>, <see cref="CurrentFile"/> or <see cref="Scope"/> changes.</summary>
    public event Action? Changed;

    /// <summary>Opens the pane on <paramref name="path"/> (or leaves it open with no file selected when <see langword="null"/>), unscoped.</summary>
    /// <param name="path">The file to open, or <see langword="null"/>.</param>
    public void OpenFile(LibraryPath? path)
    {
        this.CurrentFile = path;
        this.Scope = null;
        this.IsOpen = true;
        this.Changed?.Invoke();
    }

    /// <summary>Opens the pane narrowed to one Library Root (the sidebar's per-root rows), with no file selected.</summary>
    /// <param name="scope">The root, at its own top level, to narrow the pane's tree to.</param>
    public void OpenRoot(LibraryLocation scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        this.CurrentFile = null;
        this.Scope = scope;
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
