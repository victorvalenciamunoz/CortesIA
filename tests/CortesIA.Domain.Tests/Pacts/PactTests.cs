using CortesIA.Domain.Actions;
using CortesIA.Domain.Chronicle;
using CortesIA.Domain.Courts;
using CortesIA.Domain.Game;
using CortesIA.Domain.Pacts;
using CortesIA.Domain.Resolution;
using static CortesIA.Domain.Tests.States;

namespace CortesIA.Domain.Tests.Pacts;

public class PactTests
{
    private static readonly Trade Terms = new(CourtId.North, Resource.Grain, 2, Resource.Gold, 1);

    private static GameState Signed(GameState state, PactKind kind, int turns, Trade? terms = null) =>
        PactRules.Sign(state, new PactProposal(kind, CourtId.Player, CourtId.North, turns, terms)).State;

    private static int Prestige(TurnResult result, CourtId id) => result.State.Court(id).Prestige;

    [Fact]
    public void Signed_pact_binds_from_the_current_turn()
    {
        var state = GameState.Start() with { Turn = 4 };

        var (signed, signedEvent) = PactRules.Sign(state, new PactProposal(PactKind.NonAggression, CourtId.Player, CourtId.North, 3));

        var pact = Assert.Single(signed.Pacts);
        Assert.Equal((4, 6), (pact.StartTurn, pact.EndTurn));
        Assert.Equal(new PactSigned(4, pact), signedEvent);
    }

    [Theory]
    [InlineData(PactKind.NonAggression, CourtId.Player, 3, false, "No puedes pactar contigo mismo.")]
    [InlineData(PactKind.NonAggression, CourtId.North, 0, false, "Un pacto dura al menos 1 turno.")]
    [InlineData(PactKind.NonAggression, CourtId.North, 3, true, "Un pacto de no agresión no lleva términos de comercio.")]
    [InlineData(PactKind.RecurringTrade, CourtId.North, 3, false, "Un pacto de comercio necesita los términos del intercambio.")]
    [InlineData(PactKind.RecurringTrade, CourtId.South, 3, true, "Los términos del intercambio deben ser con Sur.")]
    public void Invalid_proposals_explain_why(PactKind kind, CourtId to, int turns, bool withTerms, string reason)
    {
        var result = PactRules.Validate(GameState.Start(), new PactProposal(kind, CourtId.Player, to, turns, withTerms ? Terms : null));

        Assert.False(result.IsValid);
        Assert.Equal(reason, result.Reason);
    }

    [Fact]
    public void Pact_must_end_by_the_last_turn()
    {
        var state = GameState.Start() with { Turn = 14 };

        var result = PactRules.Validate(state, new PactProposal(PactKind.NonAggression, CourtId.Player, CourtId.North, 3));

        Assert.Equal("El pacto acabaría en el turno 16 y la partida termina en el 15.", result.Reason);
    }

    [Fact]
    public void Only_one_pact_of_each_kind_per_pair()
    {
        var state = Signed(GameState.Start(), PactKind.NonAggression, 3);

        var again = PactRules.Validate(state, new PactProposal(PactKind.NonAggression, CourtId.North, CourtId.Player, 2));
        var trade = PactRules.Validate(state, new PactProposal(PactKind.RecurringTrade, CourtId.Player, CourtId.North, 2, Terms));

        Assert.Equal("Ya hay un pacto de no agresión con Jugador.", again.Reason);
        Assert.True(trade.IsValid);
    }

    [Fact]
    public void Kept_pact_stays_active_until_its_last_turn_and_then_rewards_both()
    {
        var state = Signed(GameState.Start(), PactKind.NonAggression, 2);
        var quiet = TurnActions(new Cultivate(), new Cultivate(), new Cultivate());

        var first = TurnResolver.Resolve(state, quiet, new FixedDice());
        var second = TurnResolver.Resolve(first.State, quiet, new FixedDice());

        Assert.Single(first.State.Pacts);
        Assert.Equal(0, Prestige(first, CourtId.Player));
        Assert.Empty(second.State.Pacts);
        Assert.Equal((1, 1, 0), (Prestige(second, CourtId.Player), Prestige(second, CourtId.North), Prestige(second, CourtId.South)));
        Assert.Contains(second.Events, e => e is PactFulfilled { Turn: 2 });
    }

    [Fact]
    public void Attacking_a_non_aggression_partner_still_fights_but_costs_prestige()
    {
        var state = Signed(GameState.Start(), PactKind.NonAggression, 3);

        var result = TurnResolver.Resolve(state, TurnActions(new Attack(CourtId.North), new Cultivate(), new Cultivate()), new FixedDice(1, 0));

        Assert.Contains(result.Events, e => e is Fought { Winner: CourtId.Player });
        Assert.Contains(result.Events, e => e is PactBroken { Breaker: CourtId.Player });
        Assert.Equal(2 - 3, Prestige(result, CourtId.Player));
        Assert.Empty(result.State.Pacts);
    }

    [Fact]
    public void Attacking_a_third_court_does_not_break_the_pact()
    {
        var state = Signed(GameState.Start(), PactKind.NonAggression, 3);

        var result = TurnResolver.Resolve(state, TurnActions(new Attack(CourtId.South), new Cultivate(), new Cultivate()), new FixedDice(0, 0));

        Assert.Single(result.State.Pacts);
    }

    [Fact]
    public void Attacking_each_other_breaks_it_for_both()
    {
        var state = Signed(GameState.Start(), PactKind.NonAggression, 3);

        var result = TurnResolver.Resolve(state, TurnActions(new Attack(CourtId.North), new Attack(CourtId.Player), new Cultivate()), new FixedDice(0, 0));

        Assert.Equal(2, result.Events.OfType<PactBroken>().Count());
        Assert.Equal((-3, -3), (Prestige(result, CourtId.Player), Prestige(result, CourtId.North)));
    }

    [Fact]
    public void Recurring_trade_executes_each_turn_and_rewards_at_the_end()
    {
        var state = Signed(GameState.Start(), PactKind.RecurringTrade, 1, Terms);

        var result = TurnResolver.Resolve(state, TurnActions(Terms, Terms.Mirrored(CourtId.Player), new Cultivate()), new FixedDice());

        Assert.Contains(result.Events, e => e is Traded);
        Assert.Contains(result.Events, e => e is PactFulfilled);
        Assert.Equal((1, 1), (Prestige(result, CourtId.Player), Prestige(result, CourtId.North)));
    }

    [Fact]
    public void Not_playing_the_agreed_trade_breaks_the_pact()
    {
        var state = Signed(GameState.Start(), PactKind.RecurringTrade, 3, Terms);

        var result = TurnResolver.Resolve(state, TurnActions(Terms, new Cultivate(), new Cultivate()), new FixedDice());

        var broken = Assert.Single(result.Events.OfType<PactBroken>());
        Assert.Equal(CourtId.North, broken.Breaker);
        Assert.Equal((0, -3), (Prestige(result, CourtId.Player), Prestige(result, CourtId.North)));
        Assert.Contains(result.Events, e => e is TradeFailed);
    }

    [Fact]
    public void Pact_with_an_eliminated_court_is_cancelled_without_reward_or_penalty()
    {
        var state = Signed(With(Court(CourtId.North, grain: 0, soldiers: 1)), PactKind.NonAggression, 3);

        var result = TurnResolver.Resolve(state, TurnActions(new Cultivate(), new Fortify(), new Cultivate()), new FixedDice());

        Assert.True(result.State.Court(CourtId.North).IsEliminated);
        Assert.Contains(result.Events, e => e is PactCancelled);
        Assert.Empty(result.State.Pacts);
        Assert.Equal(0, Prestige(result, CourtId.Player));
    }
}
