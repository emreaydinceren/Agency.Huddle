namespace Agency.Huddle.Console.Tools;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

/// <summary>
/// In-memory store of chat room names. Seeded with a hardcoded "bananas" room on construction:
/// that room is the canary proving an agent's answer came from this process rather than the
/// model's own memory.
/// </summary>
internal sealed class ChatRoomRegistry
{
    private readonly Lock gate = new Lock();

    private readonly List<string> rooms = new List<string> { "bananas" };

    public IReadOnlyList<string> List()
    {
        lock (this.gate)
        {
            return this.rooms.ToArray();
        }
    }

    public void Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("The room name must not be null or whitespace.", nameof(name));
        }

        lock (this.gate)
        {
            if (this.rooms.Any(existing => string.Equals(existing, name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"A chat room named '{name}' already exists.");
            }

            this.rooms.Add(name);
        }
    }
}
