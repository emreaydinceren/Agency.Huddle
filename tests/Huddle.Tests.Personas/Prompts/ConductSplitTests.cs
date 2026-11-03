using Agency.Huddle.App.Prompts;

namespace Agency.Huddle.Tests.Prompts;

/// <summary>
/// Pins D-17's split (Spec §1.2 table "Mechanics stay in Prompts; conduct moves to AGENTS.md"): the
/// conduct sentences of six Prompt defaults now live in the embedded default <c>Teams/AGENTS.md</c> (or, for
/// the greeting, in <c>Builtin/chief-of-staff.md</c>), while every placeholder and the mechanics stay.
/// </summary>
public sealed class ConductSplitTests
{
    private const string AgentsMdResource = "Overlays/second-brain/AGENTS.md";
    private const string ChiefResource = "Builtin/chief-of-staff.md";

    private static readonly char[] Whitespace = [' ', '\t', '\r', '\n'];

    /// <summary>Collapses every run of whitespace to one space so a wrapped line compares equal to its one-line form.</summary>
    private static string Flatten(string text) =>
        string.Join(' ', text.Split(Whitespace, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>The default text of the Prompt with the given key, whitespace-flattened.</summary>
    private static string DefaultOf(string key) =>
        Flatten(PromptCatalog.All.Single(definition => string.Equals(definition.Key, key, StringComparison.Ordinal)).Default);

    /// <summary>An embedded resource of the app assembly, whitespace-flattened.</summary>
    private static string Embedded(string resource)
    {
        using Stream stream = typeof(PromptCatalog).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Resource '{resource}' is not embedded.");
        using StreamReader reader = new(stream);
        return Flatten(reader.ReadToEnd());
    }

    /// <summary>The conduct sentences: Prompt key, the sentence as today's default words it, the destination resource.</summary>
    public static TheoryData<string, string, string> MovedSentences() => new()
    {
        // chatRules: the "@all" guidance on when to write it (the "counts as a mention" mechanic stays).
        { "systemPrompt.chatRules", "Write \"@all\" yourself only when you need every other agent to answer, never just to reply to one member.", AgentsMdResource },

        // askHuman: when to ask with options (the tool name and the end-your-Turn mechanics stay).
        { "systemPrompt.askHuman", "such as which days, what budget, or which of these matters most, and you were about to write your questions out as a list", AgentsMdResource },
        { "systemPrompt.askHuman", "Do not use it for a fact you can look up or infer, when they want your own opinion, or when they have already given you the detail.", AgentsMdResource },

        // memory: what is worth remembering.
        { "systemPrompt.memory", "When you learn something durable (a decision, a correction, a preference, a fact about the people or work you support)", AgentsMdResource },
        { "systemPrompt.memory", "Do not save what is true only for this conversation, or what you can easily look up again.", AgentsMdResource },

        // teamMemoryManager: how a reversed decision is recorded.
        { "systemPrompt.teamMemoryManager", "When a decision is reversed, replace it and note briefly that it changed.", AgentsMdResource },

        // teamMemoryMember: the list of facts worth proposing.
        { "systemPrompt.teamMemoryMember", "such as a decision and its reason, an agreed way of working or who owns what", AgentsMdResource },

        // greeting: its shape.
        { "turn.greeting", "Keep it to one Message that ends with one question.", ChiefResource },
    };

    /// <summary>The line each destination gains, as written there: an equivalent of a moved sentence.</summary>
    public static TheoryData<string, string> Destinations() => new()
    {
        { AgentsMdResource, "Write \"@all\" only when you need every other agent to answer, never just to reply to one member." },
        { AgentsMdResource, "When you need the Human's preferences, constraints or goals and were about to write your questions out as a list, ask with options they can tap instead." },
        { AgentsMdResource, "Don't ask for a fact you can look up or infer, for an opinion they want from you, or for a detail they already gave." },
        { AgentsMdResource, "When you learn something durable (a decision, a correction, a preference, a fact about the people or work you support), write it to the right topic file at once." },
        { AgentsMdResource, "Do not save what is true only for this conversation, or what you can easily look up again." },
        { AgentsMdResource, "When a decision is reversed, replace it and note briefly that it changed." },
        { AgentsMdResource, "Propose what the whole Team should keep: a decision and its reason, an agreed way of working or who owns what." },
        { ChiefResource, "Greet a new Human in one Message that ends with one question." },
    };

    /// <summary>Each Prompt default no longer holds its conduct sentence.</summary>
    [Theory]
    [MemberData(nameof(MovedSentences))]
    public void Default_NoLongerContainsConductSentence(string key, string sentence, string destination)
    {
        ArgumentNullException.ThrowIfNull(sentence);
        Assert.NotEmpty(destination);

        Assert.DoesNotContain(Flatten(sentence), DefaultOf(key), StringComparison.Ordinal);
    }

    /// <summary>The destination file carries an equivalent line for each moved sentence (an exact phrase this task writes).</summary>
    [Theory]
    [MemberData(nameof(Destinations))]
    public void Destination_ContainsEquivalentLine(string resource, string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        Assert.Contains(Flatten(line), Embedded(resource), StringComparison.Ordinal);
    }

    /// <summary>Every placeholder a split default used before still appears in it, required or not.</summary>
    [Theory]
    [InlineData("systemPrompt.askHuman", "{{askHumanTool}}")]
    [InlineData("systemPrompt.memory", "{{memoryPath}}")]
    [InlineData("systemPrompt.memory", "{{memoryIndex}}")]
    [InlineData("systemPrompt.memory", "{{memoryTool}}")]
    [InlineData("systemPrompt.memory", "{{memoryChanges}}")]
    [InlineData("systemPrompt.teamMemoryManager", "{{managedTeams}}")]
    [InlineData("systemPrompt.teamMemoryManager", "{{managedPaths}}")]
    [InlineData("systemPrompt.teamMemoryManager", "{{memoryTool}}")]
    [InlineData("systemPrompt.teamMemoryManager", "{{proposeTool}}")]
    [InlineData("systemPrompt.teamMemoryMember", "{{memberTeams}}")]
    [InlineData("systemPrompt.teamMemoryMember", "{{memberPaths}}")]
    [InlineData("systemPrompt.teamMemoryMember", "{{memoryTool}}")]
    [InlineData("systemPrompt.teamMemoryMember", "{{proposeTool}}")]
    [InlineData("turn.greeting", "{{roomLabel}}")]
    public void Default_KeepsPlaceholder(string key, string placeholder)
    {
        Assert.Contains(placeholder, DefaultOf(key), StringComparison.Ordinal);
    }

    /// <summary>The chat rules keep the mechanics Agents rely on: no reply to every message in a group Room.</summary>
    [Fact]
    public void ChatRules_KeepsGroupReplyMechanics()
    {
        string rules = DefaultOf("systemPrompt.chatRules");

        Assert.Contains("answer only when you are @-mentioned", rules, StringComparison.Ordinal);
        Assert.Contains("\"@all\" counts as a mention of every agent in the Room", rules, StringComparison.Ordinal);
    }

    /// <summary>The ask-the-Human default keeps its mechanics: end the Turn and the answer arrives as a Message.</summary>
    [Fact]
    public void AskHuman_KeepsEndTurnMechanics()
    {
        string text = DefaultOf("systemPrompt.askHuman");

        Assert.Contains("then end your Turn; the answer arrives later as a Message from the Human.", text, StringComparison.Ordinal);
    }

    /// <summary>The embedded AGENTS.md keeps its three sections.</summary>
    [Fact]
    public void AgentsMd_KeepsItsSections()
    {
        string text = Embedded(AgentsMdResource);

        Assert.Contains("## Preferences", text, StringComparison.Ordinal);
        Assert.Contains("## Voice", text, StringComparison.Ordinal);
        Assert.Contains("## Keeping our Second Brain", text, StringComparison.Ordinal);
    }
}
