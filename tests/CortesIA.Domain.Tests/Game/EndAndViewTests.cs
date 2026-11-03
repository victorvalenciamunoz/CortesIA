using CortesIA.Domain.Actions;
using CortesIA.Domain.Chronicle;
using CortesIA.Domain.Courts;
using CortesIA.Domain.Game;
using CortesIA.Domain.Pacts;
using CortesIA.Domain.Resolution;
using static CortesIA.Domain.Tests.States;

namespace CortesIA.Domain.Tests.Game;

public class EndAndViewTests
{
    private static readonly Dictionary<CourtId, GameAction> Quiet = TurnActions(new Cultivate(), new Cultivate(), new Cultivate());

    [Fact]
    public void Game_ends_after_the_last_turn_is_resolved()
    {
        var lastTurn = GameState.Start() with { Turn = GameState.LastTurn };

        Assert.False(lastTurn.IsOver);
        var result = TurnResolver.Resolve(lastTurn, Quiet, new FixedDice());

        Assert.True(result.State.IsOver);
        Assert.Throws<InvalidOperationException>(() => TurnResolver.Resolve(result.State, Quiet, new FixedDice()));
    }

    [Fact]
    public void Game_ends_early_when_only_one_court_is_left()
    {
        var state = With(
            Court(CourtId.North, grain: 0, soldiers: 0) with { IsEliminated = true },
            Court(CourtId.South, grain: 0, soldiers: 0) with { IsEliminated = true });

        Assert.True(state.IsOver);
    }

    [Fact]
    public void Highest_prestige_wins()
    {
        var state = With(Court(CourtId.North, prestige: 3), Court(CourtId.South, prestige: 1));

        var standings = Standings.Of(state);

        Assert.Equal([CourtId.North], standings.Winners);
        Assert.Equal([CourtId.North, CourtId.South, CourtId.Player], standings.Ranking.Select(c => c.Id));
    }

    [Fact]
    public void Prestige_tie_is_broken_by_grain_gold_and_soldiers()
    {
        var state = With(Court(CourtId.Player, prestige: 2, grain: 4), Court(CourtId.North, prestige: 2, gold: 9));

        Assert.Equal([CourtId.North], Standings.Of(state).Winners);
    }

    [Fact]
    public void Full_tie_is_a_shared_victory()
    {
        Assert.Equal([CourtId.Player, CourtId.North, CourtId.South], Standings.Of(GameState.Start()).Winners);
    }

    [Fact]
    public void Eliminated_court_ranks_last_whatever_its_prestige()
    {
        var state = With(Court(CourtId.Player, prestige: 9, grain: 0, soldiers: 0) with { IsEliminated = true });

        var standings = Standings.Of(state);

        Assert.Equal(CourtId.Player, standings.Ranking[^1].Id);
        Assert.DoesNotContain(CourtId.Player, standings.Winners);
    }

    [Fact]
    public void Chronicle_keeps_every_turn()
    {
        var first = TurnResolver.Resolve(GameState.Start(), Quiet, new FixedDice());
        var second = TurnResolver.Resolve(first.State, Quiet, new FixedDice());

        Assert.Equal([1, 1, 1, 2, 2, 2], second.State.Chronicle.Select(e => e.Turn));
    }

    [Fact]
    public void A_court_does_not_see_pacts_between_the_others_but_learns_when_one_is_broken()
    {
        var state = PactRules.Sign(GameState.Start(), new PactProposal(PactKind.NonAggression, CourtId.Player, CourtId.North, 3));
        var betrayal = TurnResolver.Resolve(state, TurnActions(new Attack(CourtId.North), new Cultivate(), new Cultivate()), new FixedDice(0, 0));

        var south = CourtView.For(betrayal.State, CourtId.South);
        var north = CourtView.For(betrayal.State, CourtId.North);

        Assert.DoesNotContain(south.Chronicle, e => e is PactSigned);
        Assert.Contains(south.Chronicle, e => e is PactBroken { Breaker: CourtId.Player });
        Assert.Contains(south.Chronicle, e => e is Fought);
        Assert.Contains(north.Chronicle, e => e is PactSigned);
    }

    [Fact]
    public void View_shows_own_state_the_others_own_pacts_and_legal_actions()
    {
        var state = PactRules.Sign(GameState.Start(), new PactProposal(PactKind.NonAggression, CourtId.Player, CourtId.North, 3));

        var player = CourtView.For(state, CourtId.Player);
        var south = CourtView.For(state, CourtId.South);

        Assert.Equal(CourtId.Player, player.Self.Id);
        Assert.Equal([CourtId.North, CourtId.South], player.Others.Select(c => c.Id));
        Assert.Single(player.Pacts);
        Assert.Empty(south.Pacts);
        Assert.True(player.Legal.CanRecruit);
        Assert.Equal([CourtId.North, CourtId.South], player.Legal.AttackTargets);
    }
}
