using CortesIA.Domain.Actions;
using CortesIA.Domain.Chronicle;
using CortesIA.Domain.Courts;
using CortesIA.Domain.Game;
using CortesIA.Domain.Resolution;
using static CortesIA.Domain.Tests.States;

namespace CortesIA.Domain.Tests.Resolution;

public class TurnResolverTests
{
    private static (int Grain, int Gold, int Soldiers) Stock(TurnResult result, CourtId id)
    {
        var court = result.State.Court(id);
        return (court.Grain, court.Gold, court.Soldiers);
    }

    [Fact]
    public void Each_simple_action_then_upkeep()
    {
        // 3 soldiers eat 2 grain.
        var result = TurnResolver.Resolve(GameState.Start(), TurnActions(new Cultivate(), new Recruit(), new Fortify()), new FixedDice());

        Assert.Equal((11, 5, 3), Stock(result, CourtId.Player));
        Assert.Equal((7, 3, 5), Stock(result, CourtId.North));
        Assert.Equal((8, 5, 3), Stock(result, CourtId.South));
        Assert.Equal(2, result.State.Turn);
    }

    [Fact]
    public void Cultivating_without_gold_also_gives_one_gold()
    {
        var state = With(Court(CourtId.Player, gold: 0));

        var result = TurnResolver.Resolve(state, TurnActions(new Cultivate(), new Cultivate(), new Cultivate()), new FixedDice());

        Assert.Equal((11, 1, 3), Stock(result, CourtId.Player));
        Assert.Equal((11, 5, 3), Stock(result, CourtId.North));
        Assert.Contains(new Cultivated(1, CourtId.Player, 3, 1), result.Events);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(5, 3)]
    public void Upkeep_is_half_the_soldiers_rounded_up(int soldiers, int grain) =>
        Assert.Equal(grain, TurnResolver.UpkeepCost(soldiers));

    [Fact]
    public void Mirrored_trades_execute()
    {
        var deal = new Trade(CourtId.North, Resource.Grain, 3, Resource.Gold, 2);

        var result = TurnResolver.Resolve(GameState.Start(), TurnActions(deal, deal.Mirrored(CourtId.Player), new Cultivate()), new FixedDice());

        Assert.Equal((5, 7, 3), Stock(result, CourtId.Player));
        Assert.Equal((11, 3, 3), Stock(result, CourtId.North));
        Assert.Contains(new Traded(1, CourtId.Player, deal), result.Events);
    }

    [Fact]
    public void Trade_without_a_compatible_answer_fails_and_nothing_moves()
    {
        var deal = new Trade(CourtId.North, Resource.Grain, 3, Resource.Gold, 2);
        var stingy = new Trade(CourtId.Player, Resource.Gold, 1, Resource.Grain, 3);

        var result = TurnResolver.Resolve(GameState.Start(), TurnActions(deal, stingy, new Cultivate()), new FixedDice());

        Assert.Equal((8, 5, 3), Stock(result, CourtId.Player));
        Assert.Equal((8, 5, 3), Stock(result, CourtId.North));
        Assert.Contains(new TradeFailed(1, CourtId.Player, deal, "Norte no propuso un intercambio compatible."), result.Events);
        Assert.Contains(result.Events, e => e is TradeFailed { Court: CourtId.North });
    }

    [Fact]
    public void Trade_with_a_court_that_did_something_else_fails()
    {
        var deal = new Trade(CourtId.North, Resource.Grain, 3, Resource.Gold, 2);

        var result = TurnResolver.Resolve(GameState.Start(), TurnActions(deal, new Cultivate(), new Cultivate()), new FixedDice());

        Assert.Equal((8, 5, 3), Stock(result, CourtId.Player));
        Assert.DoesNotContain(result.Events, e => e is Traded);
    }

    [Fact]
    public void Without_enough_grain_soldiers_desert_until_the_rest_are_fed()
    {
        // 1 grain feeds 2 soldiers.
        var state = With(Court(CourtId.Player, grain: 1, soldiers: 5));

        var result = TurnResolver.Resolve(state, TurnActions(new Fortify(), new Cultivate(), new Cultivate()), new FixedDice());

        Assert.Equal((0, 5, 2), Stock(result, CourtId.Player));
        Assert.Contains(new SoldiersDeserted(1, CourtId.Player, 3), result.Events);
        Assert.False(result.State.Court(CourtId.Player).IsEliminated);
    }

    [Fact]
    public void Court_left_without_soldiers_and_grain_is_eliminated()
    {
        var state = With(Court(CourtId.Player, grain: 0, soldiers: 1));

        var result = TurnResolver.Resolve(state, TurnActions(new Fortify(), new Cultivate(), new Cultivate()), new FixedDice());

        Assert.True(result.State.Court(CourtId.Player).IsEliminated);
        Assert.Contains(new CourtEliminated(1, CourtId.Player), result.Events);
    }

    [Fact]
    public void Eliminated_court_takes_no_action_and_pays_no_upkeep()
    {
        var state = With(Court(CourtId.Player, grain: 0, gold: 0, soldiers: 0) with { IsEliminated = true });
        var actions = new Dictionary<CourtId, GameAction> { [CourtId.North] = new Cultivate(), [CourtId.South] = new Cultivate() };

        var result = TurnResolver.Resolve(state, actions, new FixedDice());

        Assert.Equal((0, 0, 0), Stock(result, CourtId.Player));
        Assert.DoesNotContain(result.Events, e => e is CourtEliminated);
    }

    [Fact]
    public void Missing_or_invalid_actions_are_rejected()
    {
        var state = With(Court(CourtId.Player, gold: 1));
        var missing = new Dictionary<CourtId, GameAction> { [CourtId.North] = new Cultivate(), [CourtId.South] = new Cultivate() };

        Assert.Throws<ArgumentException>(() => TurnResolver.Resolve(state, missing, new FixedDice()));
        Assert.Throws<ArgumentException>(() => TurnResolver.Resolve(state, TurnActions(new Recruit(), new Cultivate(), new Cultivate()), new FixedDice()));
    }
}
