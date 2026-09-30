using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Pins <see cref="PersonaCommands"/>: the per-Persona list of Adapter commands currently offered,
/// replaced whole on each update, looked up without regard to case, and forgotten with its Persona.
/// </summary>
public sealed class PersonaCommandsTests
{
    private static readonly AdapterCommand Compact = new("compact", "Free up context by summarizing the conversation so far", "<optional custom summarization instructions>");

    private static readonly AdapterCommand Init = new("init", "Initialize a CLAUDE.md file", null);

    /// <summary>Builds an empty <see cref="PersonaCommands"/> that logs nowhere.</summary>
    private static PersonaCommands NewCommands() => new(NullLogger<PersonaCommands>.Instance);

    /// <summary>A Persona that has never been told of a command offers nothing.</summary>
    [Fact]
    public void Get_UnknownPersona_IsEmpty()
    {
        PersonaCommands commands = NewCommands();

        Assert.Empty(commands.Get("nova"));
    }

    /// <summary>Each <c>Set</c> replaces the whole list; there is no merge and no tombstone.</summary>
    [Fact]
    public void Set_ReplacesTheWholeList()
    {
        PersonaCommands commands = NewCommands();
        commands.Set("nova", [Compact, Init]);

        commands.Set("nova", [Init]);

        Assert.Equal([Init], commands.Get("nova"));
        Assert.Null(commands.Find("nova", "compact"));
    }

    /// <summary>A lookup ignores case and returns the Adapter's own casing, which is what must be sent back to it.</summary>
    [Fact]
    public void Find_IgnoresCase_AndReturnsTheAdaptersCasing()
    {
        PersonaCommands commands = NewCommands();
        commands.Set("Nova", [Compact]);

        AdapterCommand? found = commands.Find("NOVA", "COMPACT");

        Assert.NotNull(found);
        Assert.Equal(Compact, found);
        Assert.Equal("compact", found.Name);
    }

    /// <summary>A name the Persona does not offer finds nothing, and so does a Persona with no list.</summary>
    [Fact]
    public void Find_NotOffered_ReturnsNull()
    {
        PersonaCommands commands = NewCommands();
        commands.Set("nova", [Compact]);

        Assert.Null(commands.Find("nova", "config"));
        Assert.Null(commands.Find("luna", "compact"));
    }

    /// <summary>Two Personas on one Adapter keep separate lists.</summary>
    [Fact]
    public void Set_TwoPersonas_AreIndependent()
    {
        PersonaCommands commands = NewCommands();

        commands.Set("nova", [Compact]);
        commands.Set("luna", [Init]);

        Assert.Equal([Compact], commands.Get("nova"));
        Assert.Equal([Init], commands.Get("luna"));
    }

    /// <summary>A real change raises the event once, with the Persona's Name.</summary>
    [Fact]
    public void Set_RealChange_RaisesOnce()
    {
        PersonaCommands commands = NewCommands();
        List<string> raised = [];
        commands.CommandsChanged += raised.Add;

        commands.Set("nova", [Compact]);

        Assert.Equal(["nova"], raised);
    }

    /// <summary>An identical list raises nothing, so the second, repeated advertisement never repaints a card.</summary>
    [Fact]
    public void Set_IdenticalList_RaisesNothing()
    {
        PersonaCommands commands = NewCommands();
        commands.Set("nova", [Compact, Init]);
        List<string> raised = [];
        commands.CommandsChanged += raised.Add;

        commands.Set("nova", [Compact, Init]);

        Assert.Empty(raised);
    }

    /// <summary>An empty list for a Persona that had none is not a change.</summary>
    [Fact]
    public void Set_EmptyForPersonaWithNone_RaisesNothing()
    {
        PersonaCommands commands = NewCommands();
        List<string> raised = [];
        commands.CommandsChanged += raised.Add;

        commands.Set("nova", []);

        Assert.Empty(raised);
    }

    /// <summary>A handler may call back in: the event is raised outside the lock, so it cannot deadlock.</summary>
    [Fact]
    public void Set_HandlerReadsBack_SeesTheNewListWithoutDeadlock()
    {
        PersonaCommands commands = NewCommands();
        IReadOnlyList<AdapterCommand>? seen = null;
        commands.CommandsChanged += name => seen = commands.Get(name);

        commands.Set("nova", [Compact]);

        Assert.Equal([Compact], seen);
    }

    /// <summary>One subscriber that throws must not stop the others.</summary>
    [Fact]
    public void Set_OneHandlerThrows_OthersStillRun()
    {
        PersonaCommands commands = NewCommands();
        List<string> raised = [];
        commands.CommandsChanged += _ => throw new InvalidOperationException("a disposed card");
        commands.CommandsChanged += raised.Add;

        commands.Set("nova", [Compact]);

        Assert.Equal(["nova"], raised);
    }

    /// <summary>A renamed Persona's list moves to the new Name and the old Name offers nothing.</summary>
    [Fact]
    public void Rename_MovesTheList()
    {
        PersonaCommands commands = NewCommands();
        commands.Set("nova", [Compact]);

        commands.Rename("nova", "Stella");

        Assert.Equal([Compact], commands.Get("stella"));
        Assert.Empty(commands.Get("nova"));
    }

    /// <summary>A removed, or stopped, Persona's list is forgotten and the card is told.</summary>
    [Fact]
    public void Forget_DropsTheListAndRaises()
    {
        PersonaCommands commands = NewCommands();
        commands.Set("nova", [Compact]);
        List<string> raised = [];
        commands.CommandsChanged += raised.Add;

        commands.Forget("nova");

        Assert.Empty(commands.Get("nova"));
        Assert.Equal(["nova"], raised);
    }

    /// <summary>Forgetting a Persona that had nothing raises nothing.</summary>
    [Fact]
    public void Forget_UnknownPersona_RaisesNothing()
    {
        PersonaCommands commands = NewCommands();
        List<string> raised = [];
        commands.CommandsChanged += raised.Add;

        commands.Forget("nova");

        Assert.Empty(raised);
    }

    /// <summary>The subscriber count is the test seam that proves a disposed card unsubscribed.</summary>
    [Fact]
    public void SubscriberCount_TracksSubscribeAndUnsubscribe()
    {
        PersonaCommands commands = NewCommands();
        Action<string> handler = _ => { };

        commands.CommandsChanged += handler;
        Assert.Equal(1, commands.SubscriberCount);
        commands.CommandsChanged -= handler;

        Assert.Equal(0, commands.SubscriberCount);
    }
}
