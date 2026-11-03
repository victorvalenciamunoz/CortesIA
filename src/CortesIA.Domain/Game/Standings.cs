using CortesIA.Domain.Courts;

namespace CortesIA.Domain.Game;

/// <param name="Winners">Several when they tie on prestige and on the tiebreak; none if every court was eliminated.</param>
public sealed record Standings(IReadOnlyList<CourtState> Ranking, IReadOnlyList<CourtId> Winners)
{
    /// <summary>Highest prestige first, then grain + gold + soldiers; eliminated courts always last.</summary>
    public static Standings Of(GameState state)
    {
        var ranking = state.Courts.Values
            .OrderBy(court => court.IsEliminated)
            .ThenByDescending(court => court.Prestige)
            .ThenByDescending(Tiebreak)
            .ThenBy(court => court.Id)
            .ToList();

        var best = ranking[0];
        var winners = best.IsEliminated
            ? []
            : ranking.Where(court => !court.IsEliminated && court.Prestige == best.Prestige && Tiebreak(court) == Tiebreak(best))
                .Select(court => court.Id)
                .ToList();

        return new Standings(ranking, winners);
    }

    public static int Tiebreak(CourtState court) => court.Grain + court.Gold + court.Soldiers;
}
