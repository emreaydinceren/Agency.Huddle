using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// Pins how <see cref="RoomSession.BuildPrompt"/> treats an Adapter command: a command Turn sends the
/// bare command and nothing else, and every other prompt is guarded so it can never open with a slash,
/// which is the one thing an Adapter reads as a command (Commands spec, section 6.6 and D-9).
/// </summary>
public sealed class BuildPromptCommandTests
{
    private static WorkItem CommandItem(string name, string arguments) =>
        new(
            "room-1",
            "Room",
            "You",
            $"@nova /{name} {arguments}",
            [new CaughtUpMessage("Luna", "a missed remark")],
            WorkItemKind.Command,
            TriggerMessageId: "m1",
            OwnPostLines: ["my earlier post"],
            Command: new AdapterCommandCall(name, arguments));

    /// <summary>A command with no arguments is exactly its slash name: no Room label, no catch-up, no own posts.</summary>
    [Fact]
    public void BuildPrompt_CommandWithoutArguments_IsExactlyTheSlashName()
    {
        string prompt = RoomSession.BuildPrompt(CommandItem("compact", string.Empty), new FakePromptSource());

        Assert.Equal("/compact", prompt);
    }

    /// <summary>The arguments follow the name after one space, verbatim, including newlines.</summary>
    [Fact]
    public void BuildPrompt_CommandWithArguments_AppendsThemVerbatim()
    {
        string prompt = RoomSession.BuildPrompt(CommandItem("compact", "keep the decisions\nand the open questions"), new FakePromptSource());

        Assert.Equal("/compact keep the decisions\nand the open questions", prompt);
    }

    /// <summary>A Command item that carries no command is a programming error, not an empty prompt.</summary>
    [Fact]
    public void BuildPrompt_CommandKindWithoutACommand_Throws()
    {
        WorkItem item = new("room-1", "Room", "You", "@nova /compact", [], WorkItemKind.Command);

        Assert.Throws<InvalidOperationException>(() => RoomSession.BuildPrompt(item, new FakePromptSource()));
    }

    /// <summary>
    /// With <c>turn.message</c> hand-edited to <c>{{text}}</c>, a Message whose text starts with a slash
    /// would open the prompt with it, and another Agent's text would start a command. The guard prefixes
    /// it, so the Adapter never sees a leading slash.
    /// </summary>
    [Fact]
    public void BuildPrompt_MessageOverriddenToBareText_NeverBeginsWithASlash()
    {
        FakePromptSource prompts = new();
        prompts.SetOverride("turn.message", "{{text}}");
        WorkItem item = new("room-1", "Room", "Luna", "/config", []);

        string prompt = RoomSession.BuildPrompt(item, prompts);

        Assert.Equal($"{RoomSession.CommandGuardMarker}/config", prompt);
    }

    /// <summary>
    /// Whitespace in front of the slash does not get past the guard: whether an Adapter trims before it
    /// looks for a command is not something this code can know, so a leading space or newline is treated
    /// as the slash it may turn out to be.
    /// </summary>
    [Theory]
    [InlineData(" {{text}}")]
    [InlineData("\n{{text}}")]
    public void BuildPrompt_WhitespaceThenASlash_IsGuardedToo(string template)
    {
        ArgumentNullException.ThrowIfNull(template);
        FakePromptSource prompts = new();
        prompts.SetOverride("turn.message", template);
        WorkItem item = new("room-1", "Room", "Luna", "/config", []);

        string prompt = RoomSession.BuildPrompt(item, prompts);

        Assert.Equal($"{RoomSession.CommandGuardMarker}{template.Replace("{{text}}", "/config", StringComparison.Ordinal)}", prompt);
    }

    /// <summary>The same guard covers a Greeting whose prompt has been overridden to open with a slash.</summary>
    [Fact]
    public void BuildPrompt_GreetingOverriddenToStartWithASlash_NeverBeginsWithASlash()
    {
        FakePromptSource prompts = new();
        prompts.SetOverride("turn.greeting", "/init {{roomLabel}}");
        WorkItem item = new("room-1", "Room", string.Empty, string.Empty, [], WorkItemKind.Greeting);

        string prompt = RoomSession.BuildPrompt(item, prompts);

        Assert.False(prompt.StartsWith('/'));
    }

    /// <summary>A prompt that already opens with something else is returned exactly as before: the guard adds nothing.</summary>
    [Fact]
    public void BuildPrompt_OrdinaryMessage_IsNotChangedByTheGuard()
    {
        WorkItem item = new("room-1", "Room", "You", "/compact", []);

        string prompt = RoomSession.BuildPrompt(item, new FakePromptSource());

        Assert.Equal("[Room: Room (id: room-1)] You: /compact", prompt);
    }
}
