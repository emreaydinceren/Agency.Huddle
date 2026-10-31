using Agency.Huddle.App.Acp.Sessions;

namespace Agency.Huddle.Tests.Acp.Fakes;

/// <summary>A hand-written <see cref="ISessionSettings"/> test double whose <see cref="Current"/> a test sets, and changes between Turns.</summary>
internal sealed class FakeSessionSettings : ISessionSettings
{
    /// <summary>Creates the double with the given settings in force.</summary>
    /// <param name="current">The settings <see cref="Current"/> starts with.</param>
    public FakeSessionSettings(SessionSettings current)
    {
        this.Current = current;
    }

    /// <summary>The settings now in force; a test assigns a new value to simulate the user changing them.</summary>
    public SessionSettings Current { get; set; }
}
