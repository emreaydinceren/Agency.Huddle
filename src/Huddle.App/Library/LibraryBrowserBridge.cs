namespace Agency.Huddle.App.Library;

/// <summary>
/// What the Library's two halves say to each other when they are mounted apart: the tree in the Sidebar and the
/// document on the Library page. One per circuit. Each half registers the callbacks it answers and the other half
/// calls them; the last half mounted wins, and a half that goes away unregisters only its own.
/// </summary>
internal sealed class LibraryBrowserBridge
{
    /// <summary>The file the document half has open, or <see langword="null"/> when none is.</summary>
    public LibraryPath? OpenPath { get; private set; }

    /// <summary>Set by the tree half: told which file the document opened, so it can reveal and select it.</summary>
    public Func<LibraryPath?, Task>? OpenPathObserver { get; set; }

    /// <summary>Set by the document half: asked to settle unsaved edits before the page shows another file; answers <see langword="false"/> when the Human chose to stay.</summary>
    public Func<Task<bool>>? TryLeave { get; set; }

    /// <summary>Set by the document half: asked whether the item about to change may, since an unsaved edit in it has to be settled first.</summary>
    public Func<LibraryPath, Task<bool>>? BeforeChange { get; set; }

    /// <summary>Set by the document half: told what the tree's file operation changed, so the open file follows a rename or move.</summary>
    public Func<LibraryChange, Task>? ChangeObserver { get; set; }

    /// <summary>Records the document half's open file and tells the tree half.</summary>
    /// <param name="path">The open file, or <see langword="null"/> when none is.</param>
    public async Task PublishOpenPathAsync(LibraryPath? path)
    {
        this.OpenPath = path;
        if (this.OpenPathObserver is { } observer)
        {
            await observer(path).ConfigureAwait(false);
        }
    }

    /// <summary>Asks the document half to settle its unsaved edits, so the tree can open another file.</summary>
    /// <returns><see langword="false"/> when the document refused to leave.</returns>
    public async Task<bool> TryLeaveAsync()
    {
        return this.TryLeave is not { } leave || await leave().ConfigureAwait(false);
    }

    /// <summary>Asks the document half whether <paramref name="item"/> may be renamed, moved or deleted.</summary>
    /// <param name="item">The item about to change.</param>
    /// <returns><see langword="false"/> when the document refused to leave.</returns>
    public async Task<bool> CanChangeAsync(LibraryPath item)
    {
        return this.BeforeChange is not { } guard || await guard(item).ConfigureAwait(false);
    }

    /// <summary>Tells the document half what a file operation changed.</summary>
    /// <param name="change">The change.</param>
    public async Task PublishChangeAsync(LibraryChange change)
    {
        if (this.ChangeObserver is { } observer)
        {
            await observer(change).ConfigureAwait(false);
        }
    }
}
