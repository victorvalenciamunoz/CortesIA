using CortesIA.Domain.Courts;
using CortesIA.Domain.Game;

namespace CortesIA.Domain.Actions;

/// <summary>
/// What a court may do this turn, described as data rather than as a closed list:
/// trades have too many combinations to enumerate, so they are given as ranges.
/// Cultivate and Fortify are always allowed while the court can act.
/// </summary>
public sealed record LegalActions(
    bool CanAct,
    bool CanRecruit,
    IReadOnlyList<CourtId> AttackTargets,
    IReadOnlyList<CourtId> TradePartners,
    int MaxGrainToGive,
    int MaxGoldToGive)
{
    public static LegalActions For(GameState state, CourtId court)
    {
        var self = state.Court(court);
        if (self.IsEliminated)
        {
            return new LegalActions(false, false, [], [], 0, 0);
        }

        var alive = state.Others(court).Where(other => !state.Court(other).IsEliminated).ToList();
        return new LegalActions(
            CanAct: true,
            CanRecruit: self.Gold >= ActionValidator.RecruitCost,
            AttackTargets: self.Soldiers >= 1 ? alive : [],
            TradePartners: self.Grain + self.Gold >= 1 ? alive : [],
            MaxGrainToGive: self.Grain,
            MaxGoldToGive: self.Gold);
    }
}
