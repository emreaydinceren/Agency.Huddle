using Agency.Huddle.App.Acp.Sessions.TurnHooks;

namespace Agency.Huddle.Tests.Acp.Sessions.TurnHooks;

/// <summary>Pins the one cast <see cref="TurnHook{TCarry}"/> does of the carry (Spec §6.10): a wrong type is loud, a missing one is null.</summary>
public sealed class TurnHookBaseTests
{
    private static readonly TurnHookScope Scope = new("nova", true, true, []);

    /// <summary>A carry of the wrong type throws rather than arriving as null, so the pipeline's Warning can name the hook.</summary>
    [Fact]
    public async Task AfterTurn_WrongTypedCarry_ThrowsInvalidCast()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        CarryHook hook = new();

        await Assert.ThrowsAsync<InvalidCastException>(() =>
            ((ITurnHook)hook).AfterTurnAsync(new TurnEnded(Scope, "room-1", null, []), new TurnAddition(null, 42), ct));
    }

    /// <summary>A null addition reaches the typed After as a null carry.</summary>
    [Fact]
    public async Task AfterTurn_NullAddition_GivesANullCarry()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        CarryHook hook = new();

        _ = await ((ITurnHook)hook).AfterTurnAsync(new TurnEnded(Scope, "room-1", null, []), null, ct);

        Assert.True(hook.Called);
        Assert.Null(hook.Seen);
    }

    /// <summary>A carry of the right type reaches the typed After unchanged.</summary>
    [Fact]
    public async Task AfterTurn_RightTypedCarry_ArrivesUnchanged()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        CarryHook hook = new();

        _ = await ((ITurnHook)hook).AfterTurnAsync(new TurnEnded(Scope, "room-1", null, []), new TurnAddition(null, "carried"), ct);

        Assert.Equal("carried", hook.Seen);
    }

    /// <summary>A hook with a string carry that records what its After was handed.</summary>
    private sealed class CarryHook : TurnHook<string>
    {
        /// <summary>Gets a value indicating whether After ran.</summary>
        public bool Called { get; private set; }

        /// <summary>Gets the carry After was handed.</summary>
        public string? Seen { get; private set; }

        /// <inheritdoc />
        public override string Name => "carry";

        /// <inheritdoc />
        public override bool AppliesTo(TurnHookScope scope) => true;

        /// <inheritdoc />
        public override Task<TurnAddition?> BeforeTurnAsync(TurnStarting turn, CancellationToken cancellationToken) =>
            Task.FromResult<TurnAddition?>(null);

        /// <inheritdoc />
        protected override Task<string?> AfterTurnAsync(TurnEnded turn, string? carry, CancellationToken cancellationToken)
        {
            this.Called = true;
            this.Seen = carry;
            return Task.FromResult(turn.State);
        }
    }
}
