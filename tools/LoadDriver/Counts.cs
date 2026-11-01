using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tools.LoadDriver;

/// <summary>What the driver received, by envelope kind; written by the single reader task only.</summary>
internal sealed class Counts
{
    /// <summary>Gets the number of <see cref="MessagePosted"/> envelopes.</summary>
    public int Messages { get; private set; }

    /// <summary>Gets the number of <see cref="MessageDelta"/> envelopes.</summary>
    public int Deltas { get; private set; }

    /// <summary>Gets the number of any other envelope.</summary>
    public int Other { get; private set; }

    /// <summary>Counts one envelope.</summary>
    public void Record(ProtocolMessage message)
    {
        switch (message)
        {
            case MessagePosted:
                this.Messages++;
                break;
            case MessageDelta:
                this.Deltas++;
                break;
            default:
                this.Other++;
                break;
        }
    }
}
