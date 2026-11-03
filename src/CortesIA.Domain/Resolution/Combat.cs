using CortesIA.Domain.Courts;

namespace CortesIA.Domain.Resolution;

public sealed record CombatOutcome(
    CourtState Attacker,
    CourtState Defender,
    int AttackerForce,
    int DefenderForce,
    CourtId? Winner,
    int StolenGrain,
    int StolenGold);

/// <summary>
/// One fight, as a pure function of both courts and the two rolls.
/// Kept apart from the turn so it can also be used to estimate odds without playing.
/// </summary>
public static class Combat
{
    public const int PrestigeForVictory = 2;

    /// <param name="mutual">Both courts attacked each other: a tie hurts both and the winner plunders either way.</param>
    public static CombatOutcome Fight(
        CourtState attacker, CourtState defender, bool defenderFortified, bool mutual, int attackerRoll, int defenderRoll)
    {
        var attackerForce = attacker.Soldiers + attackerRoll;
        var defenderForce = defender.Soldiers * (defenderFortified ? 2 : 1) + defenderRoll;

        if (mutual && attackerForce == defenderForce)
        {
            return new CombatOutcome(Lose(attacker, 1), Lose(defender, 1), attackerForce, defenderForce, null, 0, 0);
        }

        // Ties go to the defender.
        var attackerWins = attackerForce > defenderForce;
        var (winner, loser) = attackerWins ? (attacker, defender) : (defender, attacker);

        // Only an attacker plunders; in a mutual fight both sides are attackers.
        var plunders = attackerWins || mutual;
        var grain = plunders ? loser.Grain / 2 : 0;
        var gold = plunders ? loser.Gold / 2 : 0;

        winner = Lose(winner, 1) with
        {
            Grain = winner.Grain + grain,
            Gold = winner.Gold + gold,
            Prestige = winner.Prestige + (plunders ? PrestigeForVictory : 0),
        };
        loser = Lose(loser, 2) with { Grain = loser.Grain - grain, Gold = loser.Gold - gold };

        return attackerWins
            ? new CombatOutcome(winner, loser, attackerForce, defenderForce, winner.Id, grain, gold)
            : new CombatOutcome(loser, winner, attackerForce, defenderForce, winner.Id, grain, gold);
    }

    private static CourtState Lose(CourtState court, int soldiers) =>
        court with { Soldiers = Math.Max(0, court.Soldiers - soldiers) };
}
