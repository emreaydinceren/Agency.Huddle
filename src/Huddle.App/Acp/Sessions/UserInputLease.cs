namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>
/// One open question on a Turn, taken with <see cref="RoomSession.TryBeginUserInput"/>. While any lease
/// on a Turn is open the Turn's idle watchdog is paused; disposing the lease ends the pause for that
/// question, and does it once however many times it is disposed.
/// </summary>
internal sealed class UserInputLease : IDisposable
{
    private readonly Action release;
    private int disposed;

    /// <summary>Initializes a new instance of the <see cref="UserInputLease"/> class.</summary>
    /// <param name="roomId">The Room of the Turn this lease is on.</param>
    /// <param name="release">Runs once, on the first dispose.</param>
    /// <param name="token">Cancelled when the Turn ends for any reason, whatever the lease's own state.</param>
    public UserInputLease(string roomId, Action release, CancellationToken token)
    {
        this.RoomId = roomId;
        this.release = release;
        this.Token = token;
    }

    /// <summary>The Room of the Turn this lease is on.</summary>
    public string RoomId { get; }

    /// <summary>Cancelled when the Turn ends, is stopped or times out, or the session shuts down.</summary>
    public CancellationToken Token { get; }

    /// <summary>Ends the pause this lease holds. A second call does nothing.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) == 0)
        {
            this.release();
        }
    }
}
