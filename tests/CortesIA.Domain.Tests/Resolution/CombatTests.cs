using CortesIA.Domain.Actions;
using CortesIA.Domain.Chronicle;
using CortesIA.Domain.Courts;
using CortesIA.Domain.Game;
using CortesIA.Domain.Resolution;
using static CortesIA.Domain.Tests.States;

namespace CortesIA.Domain.Tests.Resolution;

public class CombatTests
{
    private static (int Grain, int Gold, int Soldiers, int Prestige) Stock(CourtState court) =>
        (court.Grain, court.Gold, court.Soldiers, court.Prestige);

    private static readonly CourtState Attacker = Court(CourtId.Player);
    private static readonly CourtState Defender = Court(CourtId.North, grain: 7);

    [Fact]
    public void Winning_attacker_plunders_half_rounded_down_and_gains_prestige()
    {
        var outcome = Combat.Fight(Attacker, Defender, defenderFortified: false, mutual: false, attackerRoll: 1, defenderRoll: 0);

        Assert.Equal((4, 3), (outcome.AttackerForce, outcome.DefenderForce));
        Assert.Equal(CourtId.Player, outcome.Winner);
        Assert.Equal((3, 2), (outcome.StolenGrain, outcome.StolenGold));
        Assert.Equal((13, 7, 2, 2), Stock(outcome.Attacker));
        Assert.Equal((4, 3, 1, 0), Stock(outcome.Defender));
    }

    [Fact]
    public void Tie_goes_to_the_defender_who_plunders_nothing()
    {
        var outcome = Combat.Fight(Attacker, Defender, defenderFortified: false, mutual: false, attackerRoll: 0, defenderRoll: 0);

        Assert.Equal(CourtId.North, outcome.Winner);
        Assert.Equal((10, 5, 1, 0), Stock(outcome.Attacker));
        Assert.Equal((7, 5, 2, 0), Stock(outcome.Defender));
    }

    [Fact]
    public void Fortified_defence_counts_double()
    {
        var strong = Attacker with { Soldiers = 5 };

        var open = Combat.Fight(strong, Defender, defenderFortified: false, mutual: false, attackerRoll: 0, defenderRoll: 0);
        var fortified = Combat.Fight(strong, Defender, defenderFortified: true, mutual: false, attackerRoll: 0, defenderRoll: 0);

        Assert.Equal(CourtId.Player, open.Winner);
        Assert.Equal((5, 6), (fortified.AttackerForce, fortified.DefenderForce));
        Assert.Equal(CourtId.North, fortified.Winner);
    }

    [Fact]
    public void Mutual_tie_hurts_both_and_nobody_plunders()
    {
        var outcome = Combat.Fight(Attacker, Defender, defenderFortified: false, mutual: true, attackerRoll: 0, defenderRoll: 0);

        Assert.Null(outcome.Winner);
        Assert.Equal((10, 5, 2, 0), Stock(outcome.Attacker));
        Assert.Equal((7, 5, 2, 0), Stock(outcome.Defender));
    }

    [Fact]
    public void In_a_mutual_fight_the_second_court_can_win_and_plunder()
    {
        var outcome = Combat.Fight(Attacker, Defender, defenderFortified: false, mutual: true, attackerRoll: -1, defenderRoll: 0);

        Assert.Equal(CourtId.North, outcome.Winner);
        Assert.Equal((5, 3, 1, 0), Stock(outcome.Attacker));
        Assert.Equal((12, 7, 2, 2), Stock(outcome.Defender));
    }

    [Fact]
    public void Soldiers_never_go_below_zero()
    {
        var weak = Defender with { Soldiers = 1 };

        var outcome = Combat.Fight(Attacker, weak, defenderFortified: false, mutual: false, attackerRoll: 0, defenderRoll: 0);

        Assert.Equal(0, outcome.Defender.Soldiers);
    }

    [Fact]
    public void The_example_turn_from_the_explanation()
    {
        // Player recruits, North cultivates, South attacks North and rolls +1 against North's 0.
        var result = TurnResolver.Resolve(GameState.Start(), TurnActions(new Recruit(), new Cultivate(), new Attack(CourtId.North)), new FixedDice(1, 0));

        Assert.Equal((7, 3, 5, 0), Stock(result.State.Court(CourtId.Player)));
        Assert.Equal((6, 3, 1, 0), Stock(result.State.Court(CourtId.North)));
        Assert.Equal((15, 7, 2, 2), Stock(result.State.Court(CourtId.South)));
        Assert.Contains(new Fought(1, CourtId.South, CourtId.North, false, false, 4, 3, CourtId.South, 6, 2), result.Events);
    }

    [Fact]
    public void Soldiers_recruited_this_turn_already_defend()
    {
        // 3 + 1 against 5 − 1: a tie, so the defender wins. Without recruiting, North would have lost.
        var result = TurnResolver.Resolve(GameState.Start(), TurnActions(new Attack(CourtId.North), new Recruit(), new Cultivate()), new FixedDice(1, -1));

        Assert.Contains(result.Events, e => e is Fought { Winner: CourtId.North, AttackerForce: 4, DefenderForce: 4 });
    }

    [Fact]
    public void Attacking_each_other_is_a_single_fight_with_no_fortification()
    {
        var result = TurnResolver.Resolve(GameState.Start(), TurnActions(new Attack(CourtId.North), new Attack(CourtId.Player), new Cultivate()), new FixedDice(0, 0));

        var fight = Assert.Single(result.Events.OfType<Fought>());
        Assert.True(fight.Mutual);
        Assert.Null(fight.Winner);
    }

    [Fact]
    public void Second_fight_uses_the_state_left_by_the_first()
    {
        // Player attacks North and loses a tie (North keeps 2 soldiers); then South attacks those 2 and wins 3 to 2.
        var result = TurnResolver.Resolve(GameState.Start(), TurnActions(new Attack(CourtId.North), new Cultivate(), new Attack(CourtId.North)), new FixedDice(0, 0, 0, 0));

        var fights = result.Events.OfType<Fought>().ToList();
        Assert.Equal([CourtId.Player, CourtId.South], fights.Select(f => f.Attacker));
        Assert.Equal((3, 2), (fights[1].AttackerForce, fights[1].DefenderForce));
        Assert.Equal(0, result.State.Court(CourtId.North).Soldiers);
    }

    [Fact]
    public void Fight_order_is_drawn_with_the_dice()
    {
        var result = TurnResolver.Resolve(
            GameState.Start(),
            TurnActions(new Attack(CourtId.North), new Cultivate(), new Attack(CourtId.North)),
            new FixedDice([0, 0, 0, 0], picks: [1]));

        Assert.Equal([CourtId.South, CourtId.Player], result.Events.OfType<Fought>().Select(f => f.Attacker));
    }

    [Fact]
    public void Seeded_dice_repeat_the_same_game()
    {
        var actions = TurnActions(new Attack(CourtId.North), new Attack(CourtId.South), new Attack(CourtId.Player));

        var first = TurnResolver.Resolve(GameState.Start(), actions, new SeededDice(42));
        var second = TurnResolver.Resolve(GameState.Start(), actions, new SeededDice(42));

        Assert.Equal(first.Events, second.Events);
    }
}
