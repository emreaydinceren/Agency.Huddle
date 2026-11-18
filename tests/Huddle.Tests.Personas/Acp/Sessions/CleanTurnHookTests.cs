using System.Runtime.CompilerServices;
using Agency.Huddle.App.Acp.Sessions.TurnHooks;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// Pins the clean decision for Turn hooks (Goldfish corrections slice 2, A8): a goldfish possession's pipeline runs no hook, every
/// production hook says so through <c>AppliesTo</c>, and a new hook cannot slip in without the decision being made for it.
/// </summary>
public sealed class CleanTurnHookTests
{
    private static readonly TurnHookScope Ordinary = new("Vera", ReadsFiles: true, FileChangesOn: true, DeclaredWatches: []);

    private static readonly TurnHookScope Clean = Ordinary with { Clean = true };

    /// <summary>The hook classes the production order names, by type.</summary>
    public static TheoryData<Type> HookTypes() => [.. HookTypeList()];

    /// <summary>Every concrete hook in the App assembly.</summary>
    private static List<Type> HookTypeList() =>
    [
        .. typeof(ITurnHook).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(ITurnHook).IsAssignableFrom(type))
            .OrderBy(type => type.Name, StringComparer.Ordinal),
    ];

    /// <summary>
    /// A hook without its collaborators: <c>AppliesTo</c> and <c>Name</c> read only the scope and a constant, so none are needed,
    /// and building each real store behind four hooks would pin nothing about this decision.
    /// </summary>
    private static ITurnHook Bare(Type type) => (ITurnHook)RuntimeHelpers.GetUninitializedObject(type);

    /// <summary>A clean scope turns every production hook off, even with File Changes on.</summary>
    /// <param name="type">The hook class.</param>
    [Theory]
    [MemberData(nameof(HookTypes))]
    public void AppliesTo_CleanScope_IsFalse(Type type)
    {
        Assert.False(Bare(type).AppliesTo(Clean));
    }

    /// <summary>An ordinary scope with File Changes on keeps every production hook on, so the clean row above is the scope's doing.</summary>
    /// <param name="type">The hook class.</param>
    [Theory]
    [MemberData(nameof(HookTypes))]
    public void AppliesTo_OrdinaryScope_IsTrue(Type type)
    {
        Assert.True(Bare(type).AppliesTo(Ordinary));
    }

    /// <summary>The set of hook classes is pinned: adding one fails here until its clean decision is made and the set updated.</summary>
    [Fact]
    public void HookTypes_AreThePinnedSet()
    {
        string[] names = [.. HookTypeList().Select(type => type.Name)];

        Assert.Equal(["AgentsMdTurnHook", "FileChangesTurnHook", "HuddleMemoryTurnHook", "PlaybooksTurnHook", "TeamRenamedTurnHook"], names);
    }

    /// <summary>Every concrete hook in the App assembly is in the production order, and the order names nothing else.</summary>
    [Fact]
    public void ProductionOrder_NamesExactlyTheConcreteHooks()
    {
        List<string> hookNames = [.. HookTypeList().Select(type => Bare(type).Name).Order(StringComparer.Ordinal)];

        Assert.Equal(hookNames, [.. TurnHookPipeline.ProductionOrder.Order(StringComparer.Ordinal)]);
    }

    /// <summary>The clean copy of a pipeline holds no hook, though File Changes is on and the ordinary pipeline holds all four.</summary>
    [Fact]
    public void ForClean_HasNoHooks_WhereTheOrdinaryPipelineHasAll()
    {
        List<ITurnHook> all = [.. HookTypeList().Select(Bare)];
        TurnHookPipeline ordinary = TurnHookPipeline.ForPersona(Ordinary, all, NullLogger.Instance);

        TurnHookPipeline clean = ordinary.ForClean();

        Assert.Equal(["TeamRenamed", "AgentsMd", "Playbooks", "HuddleMemory", "FileChanges"], ordinary.HookNames);
        Assert.Empty(clean.HookNames);
    }

    /// <summary>A pipeline built for a clean scope applies no hook.</summary>
    [Fact]
    public void ForPersona_CleanScope_AppliesNone()
    {
        List<ITurnHook> all = [.. HookTypeList().Select(Bare)];

        TurnHookPipeline pipeline = TurnHookPipeline.ForPersona(Clean, all, NullLogger.Instance);

        Assert.Empty(pipeline.HookNames);
    }

    /// <summary>A scope's <c>Clean</c> defaults to false, so every existing construction keeps its meaning.</summary>
    [Fact]
    public void Scope_CleanDefaultsToFalse()
    {
        Assert.False(Ordinary.Clean);
    }
}
