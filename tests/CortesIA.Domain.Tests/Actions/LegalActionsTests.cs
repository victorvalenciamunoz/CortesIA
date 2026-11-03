using CortesIA.Domain.Actions;
using CortesIA.Domain.Courts;
using CortesIA.Domain.Game;
using static CortesIA.Domain.Tests.States;

namespace CortesIA.Domain.Tests.Actions;

public class LegalActionsTests
{
    [Fact]
    public void At_start_everything_is_allowed_against_both_rivals()
    {
        var legal = LegalActions.For(GameState.Start(), CourtId.Player);

        Assert.True(legal.CanAct);
        Assert.True(legal.CanRecruit);
        Assert.Equal([CourtId.North, CourtId.South], legal.AttackTargets);
        Assert.Equal([CourtId.North, CourtId.South], legal.TradePartners);
        Assert.Equal((10, 5), (legal.MaxGrainToGive, legal.MaxGoldToGive));
    }

    [Fact]
    public void Eliminated_rivals_are_not_targets_nor_partners()
    {
        var state = With(CourtState.Initial(CourtId.South) with { IsEliminated = true });

        var legal = LegalActions.For(state, CourtId.Player);

        Assert.Equal([CourtId.North], legal.AttackTargets);
        Assert.Equal([CourtId.North], legal.TradePartners);
    }

    [Fact]
    public void Poor_court_cannot_recruit_attack_or_trade()
    {
        var state = With(new CourtState(CourtId.Player, Grain: 0, Gold: 0, Soldiers: 0, Prestige: 0));

        var legal = LegalActions.For(state, CourtId.Player);

        Assert.True(legal.CanAct);
        Assert.False(legal.CanRecruit);
        Assert.Empty(legal.AttackTargets);
        Assert.Empty(legal.TradePartners);
    }

    [Fact]
    public void Eliminated_court_can_do_nothing()
    {
        var state = With(CourtState.Initial(CourtId.Player) with { IsEliminated = true });

        Assert.False(LegalActions.For(state, CourtId.Player).CanAct);
    }

    [Theory]
    [InlineData(10, 5, 3)]
    [InlineData(0, 1, 0)]
    [InlineData(2, 0, 1)]
    public void Every_action_described_as_legal_passes_validation(int grain, int gold, int soldiers)
    {
        var state = With(new CourtState(CourtId.Player, grain, gold, soldiers, Prestige: 0));
        var legal = LegalActions.For(state, CourtId.Player);

        List<GameAction> actions = [new Cultivate(), new Fortify()];
        if (legal.CanRecruit)
        {
            actions.Add(new Recruit());
        }

        actions.AddRange(legal.AttackTargets.Select(target => new Attack(target)));
        foreach (var partner in legal.TradePartners)
        {
            if (legal.MaxGrainToGive >= 1)
            {
                actions.Add(new Trade(partner, Resource.Grain, legal.MaxGrainToGive, Resource.Gold, 1));
            }

            if (legal.MaxGoldToGive >= 1)
            {
                actions.Add(new Trade(partner, Resource.Gold, legal.MaxGoldToGive, Resource.Grain, 1));
            }
        }

        Assert.All(actions, action => Assert.True(ActionValidator.Validate(state, CourtId.Player, action).IsValid));
    }
}
